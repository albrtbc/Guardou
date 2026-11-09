import 'package:fluentui_system_icons/fluentui_system_icons.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/item_kind.dart';
import 'package:guardou/app/router/routes.dart';
import 'package:guardou/features/files/item_interactions.dart';
import 'package:guardou/features/files/item_marks.dart';

import '../../helpers/test_app.dart';

void main() {
  final shared = testEntry('shared.txt', sharedByMe: true);
  final both = testEntry('both.txt', sharedByMe: true, starred: true);
  final private = testEntry('private.txt');
  final sharedFolder = testEntry('shared folder', kind: ItemKind.folder, sharedByMe: true, starred: true);

  late MockFilesService files;

  setUp(() {
    files = stubFiles(
      folders: {
        Routes.rootFolderId: testFolder(testRoot, [both, private, shared, sharedFolder]),
      },
    );
  });

  Future<void> pumpFiles(WidgetTester tester, {Size size = const Size(1280, 800)}) => pumpRoutedApp(
    tester,
    location: Routes.files,
    size: size,
    overrides: serviceOverrides(auth: MockAuthService(), files: files),
  );

  testWidgets('shows the marks after the name in the list view', (tester) async {
    await pumpFiles(tester);

    final name = find.ancestor(of: find.text('both.txt'), matching: find.byType(ItemName));
    final shared = find.descendant(of: name, matching: find.byIcon(FluentIcons.people_24_filled));
    final star = find.descendant(of: name, matching: find.byIcon(FluentIcons.star_24_filled));
    expect(tester.getTopRight(find.text('both.txt')).dx, lessThanOrEqualTo(tester.getTopLeft(shared).dx));
    expect(tester.getTopRight(shared).dx, lessThanOrEqualTo(tester.getTopLeft(star).dx));
    expect(tester.getCenter(star).dy, closeTo(tester.getCenter(find.text('both.txt')).dy, 1));
    expect(
      find.byWidgetPredicate((widget) => widget is Icon && widget.semanticLabel == 'Shared by you'),
      findsNWidgets(3),
    );
  });

  testWidgets('shows the marks before the more button in the grid view', (tester) async {
    await pumpFiles(tester);

    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();

    for (final name in ['both.txt', 'shared folder']) {
      final tile = find.ancestor(of: find.text(name), matching: find.byType(SelectableItem));
      final indicators = find.descendant(of: tile, matching: find.byType(ItemIndicators));
      final shared = find.descendant(of: indicators, matching: find.byIcon(FluentIcons.people_24_filled));
      final star = find.descendant(of: indicators, matching: find.byIcon(FluentIcons.star_24_filled));
      final more = find.descendant(of: tile, matching: find.byTooltip('More actions'));
      expect(shared, findsOneWidget);
      expect(star, findsOneWidget);
      expect(tester.getCenter(star).dy, closeTo(tester.getCenter(more).dy, 1));
      expect(tester.getTopRight(shared).dx, lessThanOrEqualTo(tester.getTopLeft(star).dx));
      expect(tester.getTopRight(star).dx, lessThanOrEqualTo(tester.getTopLeft(more).dx));
    }
    expect(find.byType(ItemIndicators), findsNWidgets(4));
  });
}
