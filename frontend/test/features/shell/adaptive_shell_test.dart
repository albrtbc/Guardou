import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/features/shell/adaptive_shell.dart';

import '../../helpers/test_app.dart';

void main() {
  Future<void> pumpShell(WidgetTester tester, Size size) => pumpTestApp(
    tester,
    size: size,
    overrides: serviceOverrides(auth: MockAuthService()),
    child: const AdaptiveShell(location: '/suggested', child: SizedBox.shrink()),
  );

  testWidgets('compact portrait shows the bottom navigation and the New button', (tester) async {
    await pumpShell(tester, const Size(390, 844));

    expect(find.byType(NavigationBar), findsOneWidget);
    expect(find.byType(FloatingActionButton), findsOneWidget);
    expect(find.byType(NavigationRail), findsNothing);
    expect(find.text('Shared'), findsOneWidget);
  });

  testWidgets('medium shows the navigation rail', (tester) async {
    await pumpShell(tester, const Size(700, 1000));

    expect(find.byType(NavigationRail), findsOneWidget);
    expect(find.byType(NavigationBar), findsNothing);
  });

  testWidgets('expanded shows the left navigation with the New button', (tester) async {
    await pumpShell(tester, const Size(1280, 800));

    expect(find.byType(NavigationDrawer), findsOneWidget);
    expect(find.text('New'), findsOneWidget);
    expect(find.text('Shared with me'), findsOneWidget);
    expect(find.text('Trash'), findsOneWidget);
  });

  testWidgets('the New button lists the creation actions', (tester) async {
    await pumpShell(tester, const Size(1280, 800));

    await tester.tap(find.text('New'));
    await tester.pumpAndSettle();

    expect(find.text('New folder'), findsOneWidget);
    expect(find.text('File upload'), findsOneWidget);
    expect(find.text('Folder upload'), findsOneWidget);
    expect(find.text('New file'), findsNothing);
  });

  testWidgets('the New button opens a bottom sheet in portrait', (tester) async {
    await pumpShell(tester, const Size(390, 844));

    await tester.tap(find.byType(FloatingActionButton));
    await tester.pumpAndSettle();

    expect(find.byType(BottomSheet), findsOneWidget);
    expect(find.text('Folder upload'), findsOneWidget);
  });
}
