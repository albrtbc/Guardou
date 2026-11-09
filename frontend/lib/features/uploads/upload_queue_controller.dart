import 'dart:async';
import 'dart:math';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/models/conflict_policy.dart';
import '../../api/generated/models/item_entry.dart';
import '../../core/api/api_problem.dart';
import '../../core/browser/browser_files.dart';
import '../../core/browser/browser_unload_guard.dart';
import '../files/conflict_session.dart';
import '../configuration/configuration_controller.dart';
import '../files/file_views.dart';
import '../preferences/preference_choices.dart';
import '../preferences/preferences_controller.dart';
import 'transfer_rate.dart';
import 'upload_item.dart';
import 'uploads_service.dart';

final uploadQueueProvider = NotifierProvider<UploadQueueController, UploadQueueState>(UploadQueueController.new);

class UploadQueueController extends Notifier<UploadQueueState> {
  static const _bytesPerMegabyte = 1024 * 1024;
  static const _refreshDelay = Duration(seconds: 1);
  static const _notFound = 404;

  final _jobs = <int, _UploadJob>{};
  final _conflicts = <UploadConflictRequest>[];
  int _nextId = 0;
  Timer? _refresh;

  UploadsService get _service => ref.read(uploadsServiceProvider);

  @override
  UploadQueueState build() {
    listenSelf((previous, next) {
      if (previous?.isActive != next.isActive) {
        ref.read(browserUnloadGuardProvider).warn(enabled: next.isActive);
      }
    });
    ref.onDispose(() => _refresh?.cancel());
    return const UploadQueueState();
  }

  void enqueue(String folderId, List<LocalFile> files) {
    final remembered = ref.read(preferencesControllerProvider).value?.uploadConflictPolicy.policy;
    final batch = ConflictSession(_askConflict, remembered: remembered);
    final jobs = [for (final file in files) _UploadJob(_nextId++, file, folderId, batch)];
    _jobs.addAll({for (final job in jobs) job.id: job});
    state = state.withItems([
      ...state.items,
      for (final job in jobs) UploadItem(id: job.id, name: job.file.name, size: job.file.size),
    ]);
    _pump();
  }

  void pause(int id) {
    _jobs[id]?.cancelToken?.cancel();
    _update(id, (item) => item.paused());
    _pump();
  }

  void resume(int id) {
    _update(id, (item) => item.queued());
    _pump();
  }

  void cancel(int id) {
    final job = _jobs[id];
    if (job == null) {
      return;
    }
    job.cancelToken?.cancel();
    _update(id, (item) => item.cancelled());
    _forgetSession(job);
    _pump();
  }

  void cancelAll() {
    for (final item in state.items.where((item) => item.status.isPending)) {
      cancel(item.id);
    }
  }

  void clear() {
    final finished = {
      for (final item in state.items)
        if (!item.status.isPending) item.id,
    };
    for (final id in finished) {
      final job = _jobs[id];
      if (job != null) {
        _forgetSession(job);
      }
    }
    _jobs.removeWhere((id, _) => finished.contains(id));
    state = state.withItems([
      for (final item in state.items)
        if (!finished.contains(item.id)) item,
    ]);
  }

  void resolveConflict(ConflictDecision? decision) {
    _conflicts.removeAt(0).resolve(decision);
    state = state.withConflict(_conflicts.firstOrNull);
  }

  Future<ConflictDecision?> _askConflict(String name) {
    final request = UploadConflictRequest(name);
    _conflicts.add(request);
    state = state.withConflict(_conflicts.first);
    return request.decision;
  }

  void _pump() {
    final limit = ref.read(configurationControllerProvider).value?.uploadConcurrency ?? 1;
    var running = state.items.where((item) => item.status == UploadStatus.uploading).length;
    for (final item in state.items.where((item) => item.status == UploadStatus.queued).toList()) {
      if (running >= limit) {
        return;
      }
      running++;
      final job = _jobs[item.id];
      if (job != null) {
        unawaited(_run(job));
      }
    }
  }

  Future<void> _run(_UploadJob job) async {
    _update(job.id, (item) => item.started());
    final cancelToken = job.cancelToken = CancelToken();
    try {
      final resuming = job.sessionId != null;
      final sessionId = job.sessionId ??= await _startSession(job);
      if (!_isUploading(job)) {
        _forgetSessionIfCancelled(job);
        return;
      }
      if (sessionId == null) {
        _update(job.id, (item) => _unplaced(job, item));
        return;
      }
      await _send(job, sessionId, resuming ? await _service.offset(sessionId) : 0, cancelToken);
      final entry = await _complete(job, sessionId);
      if (entry == null) {
        _forgetSession(job);
      }
      job.sessionId = null;
      _update(job.id, (item) => entry == null ? _unplaced(job, item) : item.completed(entry));
      _scheduleRefresh();
    } on Object catch (error) {
      _fail(job, error);
    } finally {
      job.cancelToken = null;
      _pump();
    }
  }

  Future<String?> _startSession(_UploadJob job) => job.batch.run(job.file.name, (policy) async {
    final id = await _service.start(job.folderId, job.file, policy);
    job.policy = policy;
    return id;
  });

  Future<void> _send(_UploadJob job, String sessionId, int offset, CancelToken cancelToken) async {
    final configuration = await ref.read(configurationControllerProvider.future);
    final chunkSize = configuration.uploadChunkSizeMb * _bytesPerMegabyte;
    final rate = TransferRate(offset);
    for (var start = offset; start < job.file.size; start += chunkSize) {
      final end = min(start + chunkSize, job.file.size);
      final chunk = await job.file.readRange(start, end);
      await _service.append(
        sessionId,
        start,
        chunk,
        cancelToken: cancelToken,
        onProgress: (count, _) => _progress(job, start + count, rate),
      );
    }
  }

  Future<ItemEntry?> _complete(_UploadJob job, String sessionId) async {
    try {
      return await _service.complete(sessionId, job.policy);
    } on DioException catch (error) {
      if (!ConflictSession.isConflict(error)) {
        rethrow;
      }
    }
    return job.batch.run(job.file.name, (policy) => _service.complete(sessionId, policy));
  }

  UploadItem _unplaced(_UploadJob job, UploadItem item) => job.batch.cancelled ? item.cancelled() : item.skipped();

  void _progress(_UploadJob job, int sent, TransferRate rate) {
    final bytesPerSecond = rate.record(sent);
    if (bytesPerSecond != null && _isUploading(job)) {
      _update(job.id, (item) => item.progressed(sent, bytesPerSecond));
    }
  }

  void _fail(_UploadJob job, Object error) {
    if (!_isUploading(job)) {
      return;
    }
    if (error case DioException(error: ApiProblem(status: _notFound))) {
      job.sessionId = null;
    }
    _update(job.id, (item) => item.failed(_failureOf(error)));
  }

  UploadFailure _failureOf(Object error) => switch (error) {
    DioException(error: ApiProblem(:final codes)) when codes.contains(ConflictSession.uploadInProgressCode) =>
      UploadFailure.uploadInProgress,
    DioException(error: ApiProblem(isQuotaExceeded: true)) => UploadFailure.quotaExceeded,
    DioException(error: ApiProblem(isFileTooLarge: true)) => UploadFailure.fileTooLarge,
    DioException(error: ApiProblem(isStorageFull: true)) => UploadFailure.storageFull,
    DioException(error: ApiProblem(isTooManyUploads: true)) => UploadFailure.tooManyUploads,
    _ => UploadFailure.other,
  };

  void _forgetSessionIfCancelled(_UploadJob job) {
    if (_statusOf(job) == UploadStatus.cancelled) {
      _forgetSession(job);
    }
  }

  void _forgetSession(_UploadJob job) {
    final sessionId = job.sessionId;
    job.sessionId = null;
    if (sessionId != null) {
      unawaited(_service.cancel(sessionId).catchError((Object _) {}));
    }
  }

  void _scheduleRefresh() => _refresh ??= Timer(_refreshDelay, () {
    _refresh = null;
    refreshFileViews(ref.invalidate);
  });

  bool _isUploading(_UploadJob job) => _statusOf(job) == UploadStatus.uploading;

  UploadStatus? _statusOf(_UploadJob job) => state.items.where((item) => item.id == job.id).firstOrNull?.status;

  void _update(int id, UploadItem Function(UploadItem item) change) =>
      state = state.withItems([for (final item in state.items) item.id == id ? change(item) : item]);
}

class _UploadJob {
  _UploadJob(this.id, this.file, this.folderId, this.batch);

  final int id;
  final LocalFile file;
  final String folderId;
  final ConflictSession batch;
  String? sessionId;
  ConflictPolicy policy = ConflictPolicy.fail;
  CancelToken? cancelToken;
}
