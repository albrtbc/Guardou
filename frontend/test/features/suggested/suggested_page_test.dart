import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/item_kind.dart';
import 'package:guardou/app/router/routes.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

void main() {
  const portrait = Size(390, 844);

  final photos = testEntry('photos', kind: ItemKind.folder);
  final zeta = testEntry('zeta.txt');
  final alpha = testEntry('alpha.txt');

  late MockFilesService files;

  setUp(() {
    files = stubFiles();
    when(files.recent).thenAnswer((_) async => [photos, zeta, alpha]);
  });

  Future<ProviderContainer> pumpSuggested(WidgetTester tester, {Size size = const Size(1280, 800)}) => pumpRoutedApp(
    tester,
    location: Routes.suggested,
    size: size,
    overrides: serviceOverrides(auth: MockAuthService(), files: files),
  );

  testWidgets('sorts the recent folders and files like a folder', (tester) async {
    await pumpSuggested(tester);

    expect(find.text('Suggested'), findsWidgets);
    expect(tester.getTopLeft(find.text('photos')).dy, lessThan(tester.getTopLeft(find.text('alpha.txt')).dy));
    expect(tester.getTopLeft(find.text('alpha.txt')).dy, lessThan(tester.getTopLeft(find.text('zeta.txt')).dy));
  });

  testWidgets('selects more than one item', (tester) async {
    await pumpSuggested(tester);

    await tester.tap(find.text('alpha.txt'));
    await tester.pumpAndSettle();
    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.tap(find.text('zeta.txt'));
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pumpAndSettle();

    expect(find.text('2 selected'), findsOneWidget);
    expect(find.byTooltip('Rename'), findsOneWidget);
  });

  testWidgets('shows an empty state in portrait', (tester) async {
    when(files.recent).thenAnswer((_) async => []);

    await pumpSuggested(tester, size: portrait);

    expect(find.text('No recent items'), findsOneWidget);
    expect(find.text('Folders and files that you add or open appear here.'), findsOneWidget);
  });
}
