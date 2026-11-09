import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../api/generated/models/item_entry.dart';
import '../files/conflict_session.dart';

enum UploadStatus {
  queued,
  uploading,
  paused,
  completed,
  skipped,
  cancelled,
  failed;

  bool get isPending => this == queued || this == uploading || this == paused;
}

enum UploadFailure { uploadInProgress, quotaExceeded, fileTooLarge, storageFull, tooManyUploads, other }

@immutable
class UploadItem {
  const UploadItem({
    required this.id,
    required this.name,
    required this.size,
    this.status = UploadStatus.queued,
    this.sent = 0,
    this.bytesPerSecond = 0,
    this.failure,
    this.entry,
  });

  final int id;
  final String name;
  final int size;
  final UploadStatus status;
  final int sent;
  final double bytesPerSecond;
  final UploadFailure? failure;
  final ItemEntry? entry;

  double get progress => size == 0 ? 0 : sent / size;

  int get remaining => size - sent;

  UploadItem queued() => _as(UploadStatus.queued);

  UploadItem started() => _as(UploadStatus.uploading);

  UploadItem paused() => _as(UploadStatus.paused);

  UploadItem skipped() => _as(UploadStatus.skipped);

  UploadItem cancelled() => _as(UploadStatus.cancelled);

  UploadItem failed(UploadFailure failure) => _as(UploadStatus.failed, failure: failure);

  UploadItem completed(ItemEntry entry) => _as(UploadStatus.completed, sent: size, entry: entry);

  UploadItem progressed(int sent, double bytesPerSecond) => _as(status, sent: sent, bytesPerSecond: bytesPerSecond);

  UploadItem _as(
    UploadStatus status, {
    int? sent,
    double bytesPerSecond = 0,
    UploadFailure? failure,
    ItemEntry? entry,
  }) => UploadItem(
    id: id,
    name: name,
    size: size,
    status: status,
    sent: sent ?? this.sent,
    bytesPerSecond: bytesPerSecond,
    failure: failure,
    entry: entry,
  );
}

class UploadConflictRequest {
  UploadConflictRequest(this.name);

  final String name;
  final _decision = Completer<ConflictDecision?>();

  Future<ConflictDecision?> get decision => _decision.future;

  void resolve(ConflictDecision? decision) => _decision.complete(decision);
}

@immutable
class UploadQueueState {
  const UploadQueueState({this.items = const [], this.conflict});

  final List<UploadItem> items;
  final UploadConflictRequest? conflict;

  bool get isActive => items.any((item) => item.status.isPending);

  int get pendingCount => _count((status) => status.isPending);

  int get completedCount => _count((status) => status == UploadStatus.completed);

  int get failedCount => _count((status) => status == UploadStatus.failed);

  Duration? get timeLeft {
    final speed = items.fold(0.0, (sum, item) => sum + item.bytesPerSecond);
    final remaining = items.where((item) => item.status.isPending).fold(0, (sum, item) => sum + item.remaining);
    return speed <= 0 ? null : Duration(seconds: (remaining / speed).ceil());
  }

  UploadQueueState withItems(List<UploadItem> items) => UploadQueueState(items: items, conflict: conflict);

  UploadQueueState withConflict(UploadConflictRequest? conflict) => UploadQueueState(items: items, conflict: conflict);

  int _count(bool Function(UploadStatus status) test) => items.where((item) => test(item.status)).length;
}
