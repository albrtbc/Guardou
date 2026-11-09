import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/breadcrumb.dart';
import 'package:guardou/api/generated/models/conflict_policy.dart';
import 'package:guardou/api/generated/models/item_kind.dart';
import 'package:guardou/api/generated/models/preview_size.dart';
import 'package:guardou/app/router/app_router.dart';
import 'package:guardou/features/files/details_panel.dart';
import 'package:guardou/features/files/item_action.dart';
import 'package:guardou/features/files/item_interactions.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

void main() {
  const portrait = Size(390, 844);
  const docsCrumb = Breadcrumb(id: 'docs', name: 'docs');
  const reportsCrumb = Breadcrumb(id: 'reports', name: 'reports');

  final docs = testEntry('docs', kind: ItemKind.folder);
  final reports = testEntry('reports', parentId: 'docs', kind: ItemKind.folder);
  final year = testEntry('2026', parentId: 'reports', kind: ItemKind.folder);
  final readme = testEntry('readme.md', size: 2048);
  final small = testEntry('small.txt', parentId: 'docs', size: 10);
  final big = testEntry('big.txt', parentId: 'docs', size: 5000);
  final photos = testEntry('photos', kind: ItemKind.folder);
  final photo = testEntry(
    'photo.jpg',
    parentId: 'photos',
    hasPreview: true,
    modified: DateTime(DateTime.now().year, 3, 5),
  );
  final opened = testEntry(
    'opened.txt',
    parentId: 'photos',
    modified: DateTime(DateTime.now().year, 3, 5),
    opened: DateTime(DateTime.now().year, 3, 7),
  );
  final many = testEntry('many', kind: ItemKind.folder);
  final manyFiles = [for (var index = 0; index < 60; index++) testEntry('file-$index.txt', parentId: 'many')];
  final pixel = base64Decode(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=',
  );

  final folders = {
    'root': testFolder(testRoot, [docs, readme]),
    testRootId: testFolder(testRoot, [docs, readme]),
    'docs': testFolder(docs, [reports, small, big], ancestors: [testRootCrumb]),
    'reports': testFolder(reports, [year], ancestors: [testRootCrumb, docsCrumb]),
    '2026': testFolder(year, const [], ancestors: [testRootCrumb, docsCrumb, reportsCrumb]),
    'photos': testFolder(photos, [photo, opened], ancestors: [testRootCrumb]),
    'many': testFolder(many, manyFiles, ancestors: [testRootCrumb]),
  };

  final dialogField = find.descendant(of: find.byType(AlertDialog), matching: find.byType(TextField));

  late MockFilesService files;

  setUpAll(() {
    registerFallbackValue(PreviewSize.thumbnail);
    registerFallbackValue(CancelToken());
    registerFallbackValue(ConflictPolicy.fail);
  });

  setUp(() => files = stubFiles(folders: folders));

  Future<void> pumpFolder(WidgetTester tester, String location, {Size size = const Size(1280, 800)}) async {
    await pumpRoutedApp(
      tester,
      location: location,
      size: size,
      overrides: serviceOverrides(auth: MockAuthService(), files: files),
    );
  }

  List<String> visibleNames(WidgetTester tester, List<String> candidates) {
    final positions = {
      for (final name in candidates)
        if (find.text(name).evaluate().isNotEmpty) name: tester.getTopLeft(find.text(name).last).dy,
    };
    return positions.keys.toList()..sort((a, b) => (positions[a] ?? 0).compareTo(positions[b] ?? 0));
  }

  testWidgets('opens the root folder from the files page', (tester) async {
    await pumpFolder(tester, '/files');

    expect(find.text('My files'), findsOneWidget);
    expect(find.widgetWithText(NavigationDrawer, 'docs'), findsNothing);
    expect(find.text('readme.md'), findsOneWidget);
    expect(find.text('1 MB of 4 MB used'), findsOneWidget);
  });

  testWidgets('shows the usage against the storage quota', (tester) async {
    when(files.usage).thenAnswer((_) async => testUsage.copyWith(quotaBytes: 2 * 1024 * 1024));

    await pumpFolder(tester, '/files');

    expect(find.text('1 MB of 2 MB used'), findsOneWidget);
  });

  testWidgets('opens a deep link and lists folders first', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    expect(find.text('Name'), findsOneWidget);
    expect(find.text('Modified'), findsOneWidget);
    expect(visibleNames(tester, ['reports', 'big.txt', 'small.txt']), ['reports', 'big.txt', 'small.txt']);
  });

  testWidgets('opens a folder with a double click', (tester) async {
    final container = await pumpRoutedApp(
      tester,
      location: '/folders/docs',
      overrides: serviceOverrides(auth: MockAuthService(), files: files),
    );

    await tester.tap(find.text('reports').last);
    await tester.pump(kDoubleTapMinTime);
    await tester.tap(find.text('reports').last);
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).state.uri.path, '/folders/reports');
    verify(() => files.item('reports')).called(1);
  });

  testWidgets('selects items with a click, Ctrl and Shift', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.text('reports'));
    await tester.pumpAndSettle();
    expect(find.text('1 selected'), findsOneWidget);

    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.tap(find.text('small.txt'));
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pumpAndSettle();
    expect(find.text('2 selected'), findsOneWidget);

    await tester.tap(find.text('big.txt'));
    await tester.pumpAndSettle();
    await tester.sendKeyDownEvent(LogicalKeyboardKey.shiftLeft);
    await tester.tap(find.text('small.txt'));
    await tester.sendKeyUpEvent(LogicalKeyboardKey.shiftLeft);
    await tester.pumpAndSettle();
    expect(find.text('2 selected'), findsOneWidget);

    await tester.tap(find.byTooltip('Clear the selection'));
    await tester.pumpAndSettle();
    expect(find.textContaining('selected'), findsNothing);
  });

  testWidgets('selects all items with Ctrl+A', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.sendKeyEvent(LogicalKeyboardKey.keyA);
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pumpAndSettle();

    expect(find.text('3 selected'), findsOneWidget);
  });

  testWidgets('selects items with a drag box', (tester) async {
    await pumpFolder(tester, '/folders/docs');
    final start = tester.getCenter(find.text('small.txt')) + const Offset(300, 100);
    final end = tester.getCenter(find.text('big.txt')) + const Offset(300, 0);

    final gesture = await tester.startGesture(start, kind: PointerDeviceKind.mouse);
    await gesture.moveTo(start + const Offset(0, 10));
    await gesture.moveTo(end);
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    expect(find.text('2 selected'), findsOneWidget);
  });

  Future<TestGesture> dragTo(WidgetTester tester, String from, String to) async {
    final target = tester.getCenter(find.text(to));
    final gesture = await tester.startGesture(tester.getCenter(find.text(from)), kind: PointerDeviceKind.mouse);
    await gesture.moveBy(const Offset(0, 10));
    await tester.pump();
    await gesture.moveTo(target);
    await tester.pump();
    return gesture;
  }

  void stubMove() => when(
    () => files.move(any(), any(), any()),
  ).thenAnswer((invocation) async => testEntry(invocation.positionalArguments.first as String));

  testWidgets('moves the selected items into a folder with a drag', (tester) async {
    stubMove();
    await pumpFolder(tester, '/folders/docs');
    await tester.tap(find.text('small.txt'));
    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.tap(find.text('big.txt'));
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pumpAndSettle();

    final gesture = await dragTo(tester, 'big.txt', 'reports');
    expect(find.text('2 selected'), findsNWidgets(2));
    await gesture.up();
    await tester.pumpAndSettle();

    verify(() => files.move('small.txt', 'reports', any())).called(1);
    verify(() => files.move('big.txt', 'reports', any())).called(1);
  });

  testWidgets('moves an item that is not selected with a drag', (tester) async {
    stubMove();
    await pumpFolder(tester, '/folders/docs');
    await tester.tap(find.text('big.txt'));
    await tester.pumpAndSettle();

    final gesture = await dragTo(tester, 'small.txt', 'reports');
    expect(find.text('small.txt'), findsNWidgets(2));
    await gesture.up();
    await tester.pumpAndSettle();

    verify(() => files.move('small.txt', 'reports', any())).called(1);
    verifyNever(() => files.move('big.txt', any(), any()));
  });

  testWidgets('does not move a folder into itself', (tester) async {
    stubMove();
    await pumpFolder(tester, '/folders/docs');

    final gesture = await dragTo(tester, 'reports', 'reports');
    await gesture.up();
    await tester.pumpAndSettle();

    verifyNever(() => files.move(any(), any(), any()));
  });

  testWidgets('renames an item from the selection toolbar', (tester) async {
    when(() => files.rename('small.txt', 'notes.txt')).thenAnswer((_) async => testEntry('notes.txt', id: 'small.txt'));
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.text('small.txt'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Rename'));
    await tester.pumpAndSettle();
    await tester.enterText(dialogField, 'notes.txt');
    await tester.tap(find.widgetWithText(FilledButton, 'Rename'));
    await tester.pumpAndSettle();

    verify(() => files.rename('small.txt', 'notes.txt')).called(1);
    expect(find.text('Renamed to "notes.txt"'), findsOneWidget);
  });

  testWidgets('refuses invalid names', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.text('small.txt'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Rename'));
    await tester.pumpAndSettle();
    await tester.enterText(dialogField, 'a/b');
    await tester.tap(find.widgetWithText(FilledButton, 'Rename'));
    await tester.pumpAndSettle();

    expect(find.textContaining('without slashes'), findsOneWidget);
    verifyNever(() => files.rename(any(), any()));
  });

  testWidgets('moves items to the trash from the context menu', (tester) async {
    when(() => files.trash(['big.txt'])).thenAnswer((_) async {});
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.text('big.txt'), buttons: kSecondaryButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Move to trash'));
    await tester.pumpAndSettle();

    verify(() => files.trash(['big.txt'])).called(1);
    expect(find.text('1 item moved to the trash'), findsOneWidget);
    expect(find.text('Undo'), findsOneWidget);
  });

  testWidgets('disables actions that the user cannot do', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.tap(find.text('small.txt'));
    await tester.tap(find.text('big.txt'));
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pumpAndSettle();

    final rename = tester.widget<IconButton>(find.widgetWithIcon(IconButton, ItemAction.rename.icon));
    expect(rename.onPressed, isNull);
  });

  testWidgets('shows the details panel', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.text('big.txt'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Details').first);
    await tester.pumpAndSettle();

    expect(find.text('TXT file'), findsWidgets);
    expect(find.text('4.9 KB'), findsWidgets);
  });

  testWidgets('creates a folder from the New menu', (tester) async {
    when(() => files.createFolder('root', 'Projects')).thenAnswer((_) async => testEntry('Projects'));
    await pumpFolder(tester, '/files');

    await tester.tap(find.text('New'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('New folder'));
    await tester.pumpAndSettle();
    await tester.enterText(dialogField, 'Projects');
    await tester.tap(find.widgetWithText(FilledButton, 'Create'));
    await tester.pumpAndSettle();

    verify(() => files.createFolder('root', 'Projects')).called(1);
    expect(find.text('Folder "Projects" created'), findsOneWidget);
  });

  testWidgets('sorts with the column headers', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.text('Size'));
    await tester.pumpAndSettle();
    expect(visibleNames(tester, ['small.txt', 'big.txt']), ['small.txt', 'big.txt']);

    await tester.tap(find.text('Size'));
    await tester.pumpAndSettle();
    expect(visibleNames(tester, ['small.txt', 'big.txt']), ['big.txt', 'small.txt']);
  });

  testWidgets('switches to the grid view', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();

    expect(find.byType(SliverGrid), findsNWidgets(2));
    expect(find.byTooltip('Show as list'), findsOneWidget);
    expect(tester.getSize(find.byType(SelectableItem).first).height, 48);
  });

  testWidgets('scales the grid tiles down when the details panel opens', (tester) async {
    await pumpFolder(tester, '/folders/docs');
    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();
    int columns() => tester
        .widgetList<SliverGrid>(find.byType(SliverGrid))
        .map((grid) => (grid.gridDelegate as SliverGridDelegateWithFixedCrossAxisCount).crossAxisCount)
        .first;
    final before = columns();
    final width = tester.getSize(find.widgetWithText(SelectableItem, 'big.txt')).width;

    await tester.tap(find.byTooltip('Details').first);
    await tester.pumpAndSettle();

    expect(find.byType(DetailsPanel), findsOneWidget);
    expect(columns(), before);
    expect(tester.getSize(find.widgetWithText(SelectableItem, 'big.txt')).width, lessThan(width));
  });

  testWidgets('keeps the layout of each folder', (tester) async {
    final container = await pumpRoutedApp(
      tester,
      location: '/folders/docs',
      overrides: serviceOverrides(auth: MockAuthService(), files: files),
    );
    final router = container.read(appRouterProvider);

    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();
    router.go('/folders/reports');
    await tester.pumpAndSettle();
    final inherited = find.byType(SliverGrid).evaluate().length;
    await tester.tap(find.byTooltip('Show as list'));
    await tester.pumpAndSettle();
    router.go('/folders/docs');
    await tester.pumpAndSettle();

    expect(inherited, 2);
    expect(find.byType(SliverGrid), findsNWidgets(2));
    router.go('/folders/reports');
    await tester.pumpAndSettle();
    expect(find.byType(SliverGrid), findsNothing);
  });

  testWidgets('records the open of a folder but not of the root folder', (tester) async {
    await pumpFolder(tester, '/files');
    verifyNever(() => files.recordOpen(any()));

    await tester.tap(find.text('docs').last);
    await tester.pump(kDoubleTapMinTime);
    await tester.tap(find.text('docs').last);
    await tester.pumpAndSettle();

    verify(() => files.recordOpen('docs')).called(1);
  });

  testWidgets('shows thumbnails for files with previews', (tester) async {
    when(() => files.preview('photo.jpg', PreviewSize.thumbnail, any())).thenAnswer((_) async => pixel);

    await pumpFolder(tester, '/folders/photos');
    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();

    expect(find.byType(Image), findsOneWidget);
    expect(find.text('You created · Mar 5'), findsOneWidget);
    expect(find.text('You opened · Mar 7'), findsOneWidget);
    final card = tester.getSize(find.byType(SelectableItem).first);
    final preview = tester.getSize(find.byType(AspectRatio).first);
    expect(card.width, greaterThanOrEqualTo(250));
    expect(preview.width / preview.height, closeTo(4 / 3, 0.01));
    verify(() => files.preview('photo.jpg', PreviewSize.thumbnail, any())).called(greaterThanOrEqualTo(1));
  });

  testWidgets('retries a pending thumbnail and cancels it on navigation', (tester) async {
    CancelToken? token;
    when(() => files.preview('photo.jpg', PreviewSize.thumbnail, any())).thenAnswer((invocation) async {
      token = invocation.positionalArguments[2] as CancelToken;
      return null;
    });
    final container = await pumpRoutedApp(
      tester,
      location: '/folders/photos',
      overrides: serviceOverrides(auth: MockAuthService(), files: files),
    );

    await tester.pump(const Duration(seconds: 3));
    container.read(appRouterProvider).go('/folders/docs');
    await tester.pumpAndSettle();
    await tester.pump(const Duration(seconds: 3));

    expect(find.byType(Image), findsNothing);
    expect(token?.isCancelled, isTrue);
    verify(() => files.preview('photo.jpg', PreviewSize.thumbnail, any())).called(2);
  });

  testWidgets('keeps the scroll position of a folder', (tester) async {
    final container = await pumpRoutedApp(
      tester,
      location: '/folders/many',
      overrides: serviceOverrides(auth: MockAuthService(), files: files),
    );
    final router = container.read(appRouterProvider);
    ScrollPosition position() => tester
        .state<ScrollableState>(
          find.ancestor(of: find.byType(SelectableItem).first, matching: find.byType(Scrollable)).first,
        )
        .position;

    await tester.drag(find.text('file-0.txt'), const Offset(0, -600));
    await tester.pumpAndSettle();
    final offset = position().pixels;
    router.go('/folders/docs');
    await tester.pumpAndSettle();
    router.go('/folders/many');
    await tester.pumpAndSettle();

    expect(offset, greaterThan(0));
    expect(position().pixels, offset);
  });

  testWidgets('opens the New menu with a right click on an empty area', (tester) async {
    await pumpFolder(tester, '/folders/docs');

    await tester.tapAt(const Offset(800, 600), buttons: kSecondaryButton, kind: PointerDeviceKind.mouse);
    await tester.pumpAndSettle();

    expect(find.text('Folder upload'), findsOneWidget);
    expect(find.textContaining('selected'), findsNothing);
  });

  testWidgets('opens the context menu at the pointer', (tester) async {
    await pumpFolder(tester, '/folders/docs');
    final position = tester.getCenter(find.text('big.txt'));

    await tester.tapAt(position, buttons: kSecondaryButton);
    await tester.pumpAndSettle();

    final menu = tester.getTopLeft(find.byType(PopupMenuItem<ItemAction>).first);
    expect((menu - position).distance, lessThan(80));
  });

  testWidgets('shows an empty state', (tester) async {
    await pumpFolder(tester, '/folders/2026');

    expect(find.text('This folder is empty'), findsOneWidget);
  });

  testWidgets('shows a missing folder', (tester) async {
    await pumpFolder(tester, '/folders/missing');

    expect(find.text('Folder not found'), findsOneWidget);
    expect(find.text('Go to My files'), findsOneWidget);
  });

  testWidgets('opens a file link with the file selected', (tester) async {
    when(() => files.item('big.txt')).thenAnswer((_) async => testFolder(big, const []));
    await pumpFolder(tester, '/file/big.txt');

    expect(find.text('1 selected'), findsOneWidget);
    expect(find.byType(DetailsPanel), findsNothing);
  });

  testWidgets('portrait collapses the breadcrumbs', (tester) async {
    await pumpFolder(tester, '/folders/2026', size: portrait);

    expect(find.byTooltip('More folders'), findsOneWidget);
    expect(find.widgetWithText(InkWell, 'docs'), findsNothing);
    expect(find.widgetWithText(InkWell, 'My files'), findsOneWidget);
  });

  testWidgets('portrait opens folders with a tap and selects with a long press', (tester) async {
    final container = await pumpRoutedApp(
      tester,
      location: '/folders/docs',
      size: portrait,
      overrides: serviceOverrides(auth: MockAuthService(), files: files),
    );

    expect(find.byType(ListTile), findsNWidgets(3));
    expect(find.text('Modified'), findsNothing);

    await tester.longPress(find.text('small.txt'));
    await tester.pumpAndSettle();
    expect(find.text('1 selected'), findsOneWidget);
    expect(find.byType(Checkbox), findsNWidgets(3));

    await tester.tap(find.text('big.txt'));
    await tester.pumpAndSettle();
    expect(find.text('2 selected'), findsOneWidget);

    await tester.tap(find.byTooltip('Clear the selection'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('reports'));
    await tester.pumpAndSettle();
    expect(container.read(appRouterProvider).state.uri.path, '/folders/reports');
  });

  testWidgets('portrait grid shows at least two columns', (tester) async {
    await pumpFolder(tester, '/folders/docs', size: portrait);
    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();

    final columns = tester
        .widgetList<SliverGrid>(find.byType(SliverGrid))
        .map((grid) => (grid.gridDelegate as SliverGridDelegateWithFixedCrossAxisCount).crossAxisCount);
    expect(columns, everyElement(greaterThanOrEqualTo(2)));
  });

  testWidgets('portrait shows actions in a bottom sheet', (tester) async {
    await pumpFolder(tester, '/folders/docs', size: portrait);

    await tester.tap(find.byTooltip('More actions').last);
    await tester.pumpAndSettle();

    expect(find.byType(BottomSheet), findsOneWidget);
    expect(find.text('Make a copy'), findsOneWidget);
  });
}
