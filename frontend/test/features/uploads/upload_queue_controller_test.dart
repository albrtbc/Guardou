import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/conflict_policy.dart';
import 'package:guardou/api/generated/models/runtime_configuration.dart';
import 'package:guardou/api/generated/models/user_preferences.dart';
import 'package:guardou/core/api/api_problem.dart';
import 'package:guardou/core/browser/browser_files.dart';
import 'package:guardou/features/configuration/configuration_controller.dart';
import 'package:guardou/features/files/conflict_session.dart';
import 'package:guardou/features/preferences/preferences_controller.dart';
import 'package:guardou/features/uploads/upload_item.dart';
import 'package:guardou/features/uploads/upload_queue_controller.dart';
import 'package:guardou/features/uploads/uploads_service.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

class MockUploadsService extends Mock implements UploadsService {}

class _NoPreferences extends PreferencesController {
  @override
  Future<UserPreferences?> build() async => null;
}

class _Configuration extends ConfigurationController {
  @override
  Future<RuntimeConfiguration> build() async => testConfiguration.copyWith(uploadConcurrency: 1, uploadChunkSizeMb: 1);
}

void main() {
  const megabyte = 1024 * 1024;
  late MockUploadsService service;

  LocalFile file(String name, int size) =>
      LocalFile(name: name, size: size, readRange: (start, end) async => Uint8List(end - start));

  Future<void> appendUntilCancelled(Invocation invocation) {
    final token = invocation.namedArguments[#cancelToken] as CancelToken;
    return token.whenCancel.then((error) => throw error);
  }

  void stubAppend(Future<void> Function(Invocation invocation) answer) => when(
    () => service.append(
      any(),
      any(),
      any(),
      cancelToken: any(named: 'cancelToken'),
      onProgress: any(named: 'onProgress'),
    ),
  ).thenAnswer(answer);

  Future<ProviderContainer> queue() async {
    final container = ProviderContainer(
      overrides: [
        uploadsServiceProvider.overrideWithValue(service),
        configurationControllerProvider.overrideWith(_Configuration.new),
        preferencesControllerProvider.overrideWith(() => _NoPreferences()),
      ],
    );
    addTearDown(container.dispose);
    await container.read(configurationControllerProvider.future);
    return container;
  }

  UploadItem item(ProviderContainer container, [int index = 0]) => container.read(uploadQueueProvider).items[index];

  setUpAll(() {
    registerFallbackValue(Uint8List(0));
    registerFallbackValue(CancelToken());
    registerFallbackValue(ConflictPolicy.fail);
    registerFallbackValue(file('fallback', 0));
  });

  setUp(() {
    service = MockUploadsService();
    when(() => service.start(any(), any(), any())).thenAnswer((_) async => 'session');
    when(() => service.complete(any(), any())).thenAnswer((_) async => testEntry('a.bin'));
    when(() => service.cancel(any())).thenAnswer((_) async {});
  });

  test('sends the file in chunks of the preferred size and completes it', () async {
    stubAppend((_) async {});
    final container = await queue();

    container.read(uploadQueueProvider.notifier).enqueue('root', [file('a.bin', megabyte + 10)]);
    await pumpEventQueue();

    verify(() => service.start('root', any(), ConflictPolicy.fail)).called(1);
    final offsets = verify(
      () => service.append(
        'session',
        captureAny(),
        any(),
        cancelToken: any(named: 'cancelToken'),
        onProgress: any(named: 'onProgress'),
      ),
    ).captured;
    expect(offsets, [0, megabyte]);
    expect(item(container).status, UploadStatus.completed);
    expect(item(container).entry?.name, 'a.bin');
  });

  test('resumes a paused upload from the server offset', () async {
    stubAppend(appendUntilCancelled);
    final container = await queue();
    final notifier = container.read(uploadQueueProvider.notifier)..enqueue('root', [file('a.bin', 1024)]);
    await pumpEventQueue();

    notifier.pause(item(container).id);
    await pumpEventQueue();
    expect(item(container).status, UploadStatus.paused);

    when(() => service.offset('session')).thenAnswer((_) async => 512);
    stubAppend((_) async {});
    notifier.resume(item(container).id);
    await pumpEventQueue();

    verify(
      () => service.append(
        'session',
        512,
        any(that: hasLength(512)),
        cancelToken: any(named: 'cancelToken'),
        onProgress: any(named: 'onProgress'),
      ),
    ).called(1);
    expect(item(container).status, UploadStatus.completed);
  });

  test('runs no more uploads than the preferred concurrency', () async {
    stubAppend(appendUntilCancelled);
    final container = await queue();

    container.read(uploadQueueProvider.notifier).enqueue('root', [file('a.bin', 10), file('b.bin', 10)]);
    await pumpEventQueue();

    verify(() => service.start(any(), any(), any())).called(1);
    expect(item(container, 1).status, UploadStatus.queued);
  });

  test('cancels the session on the server', () async {
    stubAppend(appendUntilCancelled);
    final container = await queue();
    final notifier = container.read(uploadQueueProvider.notifier)..enqueue('root', [file('a.bin', 10)]);
    await pumpEventQueue();

    notifier.cancel(item(container).id);
    await pumpEventQueue();

    verify(() => service.cancel('session')).called(1);
    expect(item(container).status, UploadStatus.cancelled);
  });

  test('shows an upload in progress to the same place as a failure', () async {
    when(() => service.start(any(), any(), any())).thenThrow(
      DioException(
        requestOptions: RequestOptions(),
        error: const ApiProblem(status: 409, codes: [ConflictSession.uploadInProgressCode]),
      ),
    );
    final container = await queue();

    container.read(uploadQueueProvider.notifier).enqueue('root', [file('a.bin', 10)]);
    await pumpEventQueue();

    expect(item(container).status, UploadStatus.failed);
    expect(item(container).failure, UploadFailure.uploadInProgress);
  });

  test('shows a full storage quota as a failure', () async {
    when(() => service.start(any(), any(), any())).thenThrow(
      DioException(
        requestOptions: RequestOptions(),
        error: const ApiProblem(status: 413, codes: [ApiProblem.quotaExceededCode]),
      ),
    );
    final container = await queue();

    container.read(uploadQueueProvider.notifier).enqueue('root', [file('a.bin', 10)]);
    await pumpEventQueue();

    expect(item(container).status, UploadStatus.failed);
    expect(item(container).failure, UploadFailure.quotaExceeded);
  });

  Future<UploadFailure?> failureOfStart(ApiProblem problem) async {
    when(
      () => service.start(any(), any(), any()),
    ).thenThrow(DioException(requestOptions: RequestOptions(), error: problem));
    final container = await queue();

    container.read(uploadQueueProvider.notifier).enqueue('root', [file('a.bin', 10)]);
    await pumpEventQueue();

    expect(item(container).status, UploadStatus.failed);
    return item(container).failure;
  }

  test('shows a file above the maximum size as a failure', () async {
    final failure = await failureOfStart(const ApiProblem(status: 413, codes: [ApiProblem.fileTooLargeCode]));

    expect(failure, UploadFailure.fileTooLarge);
  });

  test('shows a full server as a failure', () async {
    final failure = await failureOfStart(const ApiProblem(status: 507, codes: [ApiProblem.storageFullCode]));

    expect(failure, UploadFailure.storageFull);
  });

  test('shows a server that fills up during a write as a failure', () async {
    stubAppend((_) async => throw DioException(requestOptions: RequestOptions(), error: const ApiProblem(status: 507)));
    final container = await queue();

    container.read(uploadQueueProvider.notifier).enqueue('root', [file('a.bin', 10)]);
    await pumpEventQueue();

    expect(item(container).failure, UploadFailure.storageFull);
  });

  test('cancels the server session of a failed upload when the finished rows are cleared', () async {
    stubAppend((_) async => throw DioException(requestOptions: RequestOptions(), error: const ApiProblem(status: 507)));
    final container = await queue();
    final notifier = container.read(uploadQueueProvider.notifier)..enqueue('root', [file('a.bin', 10)]);
    await pumpEventQueue();
    verifyNever(() => service.cancel(any()));

    notifier.clear();
    await pumpEventQueue();

    verify(() => service.cancel('session')).called(1);
    expect(container.read(uploadQueueProvider).items, isEmpty);
  });

  test('shows too many uploads in progress as a failure', () async {
    final failure = await failureOfStart(const ApiProblem(status: 429, codes: [ApiProblem.tooManyUploadsCode]));

    expect(failure, UploadFailure.tooManyUploads);
  });

  test('does not take a rate limit for too many uploads', () async {
    final failure = await failureOfStart(const ApiProblem(status: 429));

    expect(failure, UploadFailure.other);
  });
}
