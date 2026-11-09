import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/conflict_policy.dart';
import 'package:guardou/api/generated/models/item_kind.dart';
import 'package:guardou/app/router/routes.dart';
import 'package:guardou/features/files/item_marks.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

void main() {
  const portrait = Size(390, 844);

  final docs = testEntry('docs', kind: ItemKind.folder, starred: true);
  final plan = testEntry('plan.txt', starred: true);
  final notes = testEntry('notes.txt');

  late MockFilesService files;

  setUpAll(() => registerFallbackValue(ConflictPolicy.fail));

  setUp(() {
    files = stubFiles(
      folders: {
        Routes.rootFolderId: testFolder(testRoot, [notes]),
      },
    );
    when(files.starred).thenAnswer((_) async => [plan, docs]);
    when(() => files.star(any())).thenAnswer((_) async {});
    when(() => files.unstar(any())).thenAnswer((_) async {});
  });

  Future<ProviderContainer> pumpStarred(
    WidgetTester tester, {
    String location = Routes.starred,
    Size size = const Size(1280, 800),
  }) => pumpRoutedApp(
    tester,
    location: location,
    size: size,
    overrides: serviceOverrides(auth: MockAuthService(), files: files),
  );

  testWidgets('lists the starred items with folders first', (tester) async {
    await pumpStarred(tester);

    expect(find.text('Starred'), findsWidgets);
    expect(tester.getTopLeft(find.text('docs')).dy, lessThan(tester.getTopLeft(find.text('plan.txt')).dy));
    expect(
      find.descendant(of: find.byType(ItemName), matching: find.byIcon(FluentIcons.star_24_filled)),
      findsNWidgets(2),
    );
  });

  testWidgets('does not move items with a drag', (tester) async {
    await pumpStarred(tester);
    final target = tester.getCenter(find.text('docs'));

    final gesture = await tester.startGesture(tester.getCenter(find.text('plan.txt')), kind: PointerDeviceKind.mouse);
    await gesture.moveBy(const Offset(0, 10));
    await tester.pump();
    await gesture.moveTo(target);
    await tester.pump();
    expect(find.text('plan.txt'), findsOneWidget);
    await gesture.up();
    await tester.pumpAndSettle();

    verifyNever(() => files.move(any(), any(), any()));
  });

  testWidgets('shows the stars before the more button in the grid view', (tester) async {
    await pumpStarred(tester);

    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();

    expect(
      find.descendant(of: find.byType(ItemIndicators), matching: find.byIcon(FluentIcons.star_24_filled)),
      findsNWidgets(2),
    );
  });

  testWidgets('removes a star from the context menu', (tester) async {
    await pumpStarred(tester);

    await tester.tap(find.text('plan.txt'), buttons: kSecondaryButton);
    await tester.pumpAndSettle();

    expect(find.text('Add to Starred'), findsNothing);
    await tester.tap(find.text('Remove from Starred'));
    await tester.pumpAndSettle();

    verify(() => files.unstar(['plan.txt'])).called(1);
    expect(find.text('1 item removed from Starred'), findsOneWidget);
  });

  testWidgets('adds a star from the selection toolbar', (tester) async {
    await pumpStarred(tester, location: Routes.files);
    await tester.tap(find.text('notes.txt'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Add to Starred'));
    await tester.pumpAndSettle();

    verify(() => files.star(['notes.txt'])).called(1);
    expect(find.byTooltip('Remove from Starred'), findsNothing);
  });

  testWidgets('shows an empty state in portrait', (tester) async {
    when(files.starred).thenAnswer((_) async => []);

    await pumpStarred(tester, size: portrait);

    expect(find.text('No starred items'), findsOneWidget);
    expect(find.text('Add a star to folders and files that you use frequently.'), findsOneWidget);
  });
}
