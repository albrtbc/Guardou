import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/app/theme/app_theme.dart';
import 'package:guardou/features/uploads/upload_item.dart';
import 'package:guardou/features/uploads/upload_panel.dart';
import 'package:guardou/features/uploads/upload_queue_controller.dart';
import 'package:guardou/l10n/generated/app_localizations.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../helpers/test_app.dart';

class _Queue extends UploadQueueController {
  _Queue(this._state);

  final UploadQueueState _state;

  @override
  UploadQueueState build() => _state;
}

void main() {
  Future<void> pumpPanel(WidgetTester tester, UploadQueueState state) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [uploadQueueProvider.overrideWith(() => _Queue(state))],
        child: MaterialApp(
          theme: AppTheme.light(),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: const Scaffold(
            body: Align(
              alignment: Alignment.bottomRight,
              child: SizedBox(width: UploadPanel.width, child: UploadPanel(compact: false)),
            ),
          ),
        ),
      ),
    );
    await tester.pump();
  }

  testWidgets('shows the completed uploads with a check', (tester) async {
    const name = 'fish_feast_0.2.0.zip';
    await pumpPanel(
      tester,
      UploadQueueState(items: [const UploadItem(id: 0, name: name, size: 10).completed(testEntry(name))]),
    );

    expect(find.text('1 upload complete'), findsOneWidget);
    expect(find.text(name), findsOneWidget);
    expect(find.byIcon(LucideIcons.check), findsOneWidget);
  });

  testWidgets('shows the active uploads with the progress and the time left', (tester) async {
    await pumpPanel(
      tester,
      const UploadQueueState(
        items: [
          UploadItem(id: 0, name: 'a.bin', size: 2048, status: UploadStatus.uploading, sent: 1024, bytesPerSecond: 10),
        ],
      ),
    );

    expect(find.text('Uploading 1 item'), findsOneWidget);
    expect(find.text('2 minutes left'), findsOneWidget);
    expect(find.text('1 KB of 2 KB'), findsOneWidget);
  });

  for (final (failure, message) in [
    (UploadFailure.quotaExceeded, 'Not enough space.'),
    (UploadFailure.fileTooLarge, 'File is too large.'),
    (UploadFailure.storageFull, 'The server is out of space.'),
    (UploadFailure.tooManyUploads, 'Too many uploads at once. Try again soon.'),
    (UploadFailure.other, 'The upload failed'),
  ]) {
    testWidgets('explains a failed upload: $failure', (tester) async {
      await pumpPanel(
        tester,
        UploadQueueState(items: [const UploadItem(id: 0, name: 'a.bin', size: 10).failed(failure)]),
      );

      expect(find.text(message), findsOneWidget);
    });
  }

  testWidgets('shows nothing when the queue is empty', (tester) async {
    await pumpPanel(tester, const UploadQueueState());

    expect(find.byIcon(LucideIcons.x), findsNothing);
  });
}
