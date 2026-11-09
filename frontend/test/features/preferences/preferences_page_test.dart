import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/language.dart';
import 'package:guardou/api/generated/models/start_page.dart';
import 'package:guardou/api/generated/models/theme_preference.dart';
import 'package:guardou/api/generated/models/upload_conflict_policy.dart';
import 'package:guardou/api/generated/models/user_preferences.dart';
import 'package:guardou/features/preferences/preferences_page.dart';
import 'package:guardou/features/starred/starred_page.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

void main() {
  const compact = Size(390, 844);
  const saved = UserPreferences(
    theme: ThemePreference.dark,
    language: Language.en,
    startPage: StartPage.starred,
    uploadConflictPolicy: UploadConflictPolicy.rename,
  );

  late MockPreferencesService preferences;

  setUpAll(() => registerFallbackValue(testPreferences));

  setUp(() {
    preferences = stubPreferences(preferences: saved);
    when(
      () => preferences.change(any()),
    ).thenAnswer((invocation) async => invocation.positionalArguments.single as UserPreferences);
  });

  Future<void> pumpPreferences(WidgetTester tester, {Size size = const Size(1280, 800)}) => pumpRoutedApp(
    tester,
    location: '/settings',
    size: size,
    overrides: serviceOverrides(auth: MockAuthService(), preferences: preferences),
  );

  Future<void> expand(WidgetTester tester, String title) async {
    await tester.tap(find.text(title));
    await tester.pumpAndSettle();
  }

  Future<void> choose(WidgetTester tester, String current, String choice) async {
    await tester.tap(find.text(current));
    await tester.pumpAndSettle();
    await tester.tap(find.text(choice).last);
    await tester.pumpAndSettle();
  }

  Finder section(IconData icon) => find.ancestor(of: find.byIcon(icon), matching: find.byType(ExpansionTile));

  Future<void> tapIn(WidgetTester tester, Finder section, String label) async {
    final button = find.descendant(of: section, matching: find.text(label));
    await tester.ensureVisible(button);
    await tester.tap(button);
    await tester.pumpAndSettle();
  }

  MaterialApp app(WidgetTester tester) => tester.widget<MaterialApp>(find.byType(MaterialApp));

  for (final size in [const Size(1280, 800), compact]) {
    testWidgets('shows the profile settings sections at $size', (tester) async {
      await pumpPreferences(tester, size: size);

      expect(find.byType(PreferencesPage), findsOneWidget);
      expect(find.text('General'), findsOneWidget);
      expect(find.text('Uploads'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('applies the saved theme and language', (tester) async {
    await pumpPreferences(tester);

    expect(app(tester).themeMode, ThemeMode.dark);
    expect(app(tester).locale, const Locale('en'));
  });

  testWidgets('saves the general preferences and keeps the upload preference', (tester) async {
    await pumpPreferences(tester);
    await expand(tester, 'General');

    await choose(tester, 'Dark', 'Light');
    await choose(tester, 'English', 'Português (Brasil)');
    await tapIn(tester, section(FluentIcons.settings_24_regular), 'Starred');
    await tester.tap(find.text('Suggested').last);
    await tester.pumpAndSettle();
    await tapIn(tester, section(FluentIcons.settings_24_regular), 'Save');

    verify(
      () => preferences.change(
        const UserPreferences(
          theme: ThemePreference.light,
          language: Language.ptBr,
          startPage: StartPage.suggested,
          uploadConflictPolicy: UploadConflictPolicy.rename,
        ),
      ),
    ).called(1);
    expect(app(tester).themeMode, ThemeMode.light);
    expect(app(tester).locale, const Locale('pt', 'BR'));
  });

  testWidgets('saves the upload conflict policy in portrait', (tester) async {
    await pumpPreferences(tester, size: compact);
    await expand(tester, 'Uploads');

    await choose(tester, 'Keep both items', 'Skip this item');
    await tapIn(tester, section(FluentIcons.arrow_upload_24_regular), 'Save');

    verify(() => preferences.change(saved.copyWith(uploadConflictPolicy: UploadConflictPolicy.skip))).called(1);
  });

  testWidgets('opens the start page at the root', (tester) async {
    await pumpRoutedApp(
      tester,
      location: '/',
      overrides: serviceOverrides(auth: MockAuthService(), preferences: preferences),
    );

    expect(find.byType(StarredPage), findsOneWidget);
  });

  testWidgets('restores the defaults in the form without a save', (tester) async {
    await pumpPreferences(tester);
    await expand(tester, 'Uploads');

    await tapIn(tester, section(FluentIcons.arrow_upload_24_regular), 'Restore the default settings');

    expect(find.text('Ask each time'), findsOneWidget);
    verifyNever(() => preferences.change(any()));
  });

  testWidgets('opens the account settings from the account panel', (tester) async {
    await pumpRoutedApp(
      tester,
      location: '/files',
      overrides: serviceOverrides(auth: MockAuthService(), preferences: preferences),
    );

    await tester.tap(find.byTooltip('Account'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Account settings'));
    await tester.pumpAndSettle();

    expect(find.byType(PreferencesPage), findsOneWidget);
  });
}
