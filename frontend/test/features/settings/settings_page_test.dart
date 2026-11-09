import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/locked_setting.dart';
import 'package:guardou/api/generated/models/oidc_settings_change.dart';
import 'package:guardou/api/generated/models/oidc_settings_view.dart';
import 'package:guardou/api/generated/models/server_settings.dart';
import 'package:guardou/api/generated/models/server_settings_view.dart';
import 'package:guardou/api/generated/models/task_concurrency.dart';
import 'package:guardou/api/generated/models/task_kind.dart';
import 'package:guardou/app/router/app_router.dart';
import 'package:guardou/features/settings/settings_page.dart';
import 'package:guardou/features/settings/settings_section.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

void main() {
  const previews = TaskConcurrency(kind: TaskKind.preview, concurrency: 2);
  const compact = Size(390, 844);
  const oidc = OidcSettingsView(
    issuer: null,
    clientId: null,
    clientSecretSet: false,
    scopes: ['openid', 'profile', 'email'],
    userClaim: 'preferred_username',
    autoCreateUsers: true,
    passwordLogin: true,
    redirectUri: 'http://localhost/api/auth/oidc/callback',
    postLogoutRedirectUri: 'http://localhost/login',
    locked: [],
  );
  const server = ServerSettingsView(
    externalUrl: null,
    trashRetentionDays: 30,
    uploadConcurrency: 3,
    uploadChunkSizeMb: 64,
    maxFileSizeMb: null,
    minFreeSpaceMb: 1024,
    locked: [],
  );
  const concurrencyLabel = 'Generate previews: tasks at the same time';

  late MockSettingsService settings;

  setUpAll(() {
    registerFallbackValue(const ServerSettings(externalUrl: null));
    registerFallbackValue(
      const OidcSettingsChange(
        issuer: null,
        clientId: null,
        clientSecret: null,
        scopes: [],
        userClaim: '',
        autoCreateUsers: true,
        passwordLogin: true,
      ),
    );
  });

  setUp(() {
    settings = MockSettingsService();
    when(settings.taskSettings).thenAnswer((_) async => [previews]);
    when(settings.oidcSettings).thenAnswer((_) async => oidc);
    when(settings.serverSettings).thenAnswer((_) async => server);
  });

  Future<ProviderContainer> pumpSettings(WidgetTester tester, {Size size = const Size(1280, 800)}) => pumpRoutedApp(
    tester,
    location: '/admin/settings',
    size: size,
    overrides: serviceOverrides(auth: MockAuthService(), settings: settings),
  );

  Finder field(String label) => find.descendant(
    of: find.byWidgetPredicate((widget) => widget is SettingsTextField && widget.label == label),
    matching: find.byType(TextFormField),
  );

  Finder toggle(String label) => find.descendant(
    of: find.byWidgetPredicate((widget) => widget is SettingsSwitch && widget.label == label),
    matching: find.byType(Switch),
  );

  Finder button(String label) =>
      find.ancestor(of: find.text(label), matching: find.byWidgetPredicate((widget) => widget is ButtonStyleButton));

  Future<void> reveal(WidgetTester tester, Finder finder) async {
    FocusManager.instance.primaryFocus?.unfocus();
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      finder,
      200,
      scrollable: find.descendant(of: find.byType(SettingsPage), matching: find.byType(Scrollable)).first,
    );
    await tester.ensureVisible(finder);
    await tester.pumpAndSettle();
  }

  Future<void> expand(WidgetTester tester, String title) async {
    await reveal(tester, find.text(title));
    await tester.tap(find.text(title));
    await tester.pumpAndSettle();
  }

  Future<void> tapVisible(WidgetTester tester, Finder finder) async {
    await reveal(tester, finder);
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  testWidgets('opens the settings from the admin navigation with collapsed sections', (tester) async {
    final users = MockUsersService();
    when(users.list).thenAnswer((_) async => []);
    final container = await pumpRoutedApp(
      tester,
      location: '/admin/users',
      overrides: serviceOverrides(auth: MockAuthService(), users: users, settings: settings),
    );

    await tester.tap(find.text('Settings').last);
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).state.uri.path, '/admin/settings');
    expect(find.text('Server'), findsOneWidget);
    expect(find.text('Single sign-on (OIDC)'), findsOneWidget);
    expect(find.text('Task settings'), findsOneWidget);
    expect(field('External URL').hitTestable(), findsNothing);
  });

  testWidgets('saves the concurrency of a queue', (tester) async {
    when(
      () => settings.changeTaskSettings(any()),
    ).thenAnswer((invocation) async => invocation.positionalArguments.single as List<TaskConcurrency>);
    await pumpSettings(tester);
    await expand(tester, 'Task settings');

    await tester.enterText(field(concurrencyLabel), '6');
    await tapVisible(tester, button('Save').hitTestable());

    verify(() => settings.changeTaskSettings([previews.copyWith(concurrency: 6)])).called(1);
    expect(find.text('The settings are saved.'), findsOneWidget);
  });

  testWidgets('refuses a concurrency out of range', (tester) async {
    await pumpSettings(tester);
    await expand(tester, 'Task settings');

    await tester.enterText(field(concurrencyLabel), '17');
    await tapVisible(tester, button('Save').hitTestable());

    expect(find.text('Enter a number from 1 to 16.'), findsOneWidget);
    verifyNever(() => settings.changeTaskSettings(any()));
  });

  testWidgets('resets the form and restores the defaults', (tester) async {
    when(settings.taskSettingsDefaults).thenAnswer((_) async => [previews.copyWith(concurrency: 3)]);
    await pumpSettings(tester);
    await expand(tester, 'Task settings');

    await tester.enterText(field(concurrencyLabel), '9');
    await tapVisible(tester, button('Reset').hitTestable());
    expect(find.descendant(of: field(concurrencyLabel), matching: find.text('2')), findsOneWidget);

    await tapVisible(tester, button('Restore the default settings').hitTestable());
    expect(find.descendant(of: field(concurrencyLabel), matching: find.text('3')), findsOneWidget);
    verifyNever(() => settings.changeTaskSettings(any()));
  });

  testWidgets('saves the external URL', (tester) async {
    when(() => settings.changeServerSettings(any())).thenAnswer((_) async => server);
    await pumpSettings(tester);
    await expand(tester, 'Server');

    await tester.enterText(field('External URL'), ' https://files.example.com ');
    await tester.enterText(field('Trash retention (days)'), '7');
    await tapVisible(tester, button('Save').hitTestable());

    verify(
      () => settings.changeServerSettings(
        const ServerSettings(
          externalUrl: 'https://files.example.com',
          trashRetentionDays: 7,
          uploadConcurrency: 3,
          uploadChunkSizeMb: 64,
        ),
      ),
    ).called(1);
  });

  testWidgets('saves the upload settings', (tester) async {
    when(() => settings.changeServerSettings(any())).thenAnswer((_) async => server);
    await pumpSettings(tester);
    await expand(tester, 'Server');

    await tester.enterText(field('Simultaneous uploads'), '5');
    await tester.enterText(field('Chunk size (MB)'), '16');
    await tapVisible(tester, button('Save').hitTestable());

    verify(
      () => settings.changeServerSettings(
        const ServerSettings(externalUrl: null, trashRetentionDays: 30, uploadConcurrency: 5, uploadChunkSizeMb: 16),
      ),
    ).called(1);
  });

  testWidgets('saves the storage limits and clears the maximum file size', (tester) async {
    when(() => settings.changeServerSettings(any())).thenAnswer((_) async => server);
    await pumpSettings(tester);
    await expand(tester, 'Server');

    await tester.enterText(field('Maximum file size (MB)'), '500');
    await tester.enterText(field('Free space to keep (MB)'), '2048');
    await tapVisible(tester, button('Save').hitTestable());
    await tester.enterText(field('Maximum file size (MB)'), '');
    await tapVisible(tester, button('Save').hitTestable());

    verify(
      () => settings.changeServerSettings(
        const ServerSettings(externalUrl: null, maxFileSizeMb: 500, minFreeSpaceMb: 2048),
      ),
    ).called(1);
    verify(
      () => settings.changeServerSettings(const ServerSettings(externalUrl: null, minFreeSpaceMb: 2048)),
    ).called(1);
  });

  testWidgets('refuses a chunk size out of range', (tester) async {
    await pumpSettings(tester);
    await expand(tester, 'Server');

    await tester.enterText(field('Chunk size (MB)'), '91');
    await tapVisible(tester, button('Save').hitTestable());

    expect(find.text('Enter a number from 1 to 90.'), findsOneWidget);
    verifyNever(() => settings.changeServerSettings(any()));
  });

  testWidgets('refuses a trash retention out of range', (tester) async {
    await pumpSettings(tester);
    await expand(tester, 'Server');

    await tester.enterText(field('Trash retention (days)'), '0');
    await tapVisible(tester, button('Save').hitTestable());

    expect(find.text('Enter a number from 1 to 3650.'), findsOneWidget);
    verifyNever(() => settings.changeServerSettings(any()));
  });

  testWidgets('shows a locked field as read-only with the reason', (tester) async {
    when(settings.serverSettings).thenAnswer(
      (_) async => server.copyWith(
        externalUrl: 'https://env.example.com',
        locked: const [LockedSetting(field: 'externalUrl', variable: 'EXTERNAL_URL')],
      ),
    );
    await pumpSettings(tester);
    await expand(tester, 'Server');

    final externalUrl = tester.widget<TextField>(
      find.descendant(of: field('External URL'), matching: find.byType(TextField)),
    );
    expect(externalUrl.readOnly, isTrue);
    expect(
      find.text('Set by the environment variable EXTERNAL_URL. To change it, change the variable and restart the server.'),
      findsOneWidget,
    );
    final retention = tester.widget<TextField>(
      find.descendant(of: field('Trash retention (days)'), matching: find.byType(TextField)),
    );
    expect(retention.readOnly, isFalse);
  });

  testWidgets('refuses an external URL that is not HTTP', (tester) async {
    await pumpSettings(tester);
    await expand(tester, 'Server');

    await tester.enterText(field('External URL'), 'files.example.com');
    await tapVisible(tester, button('Save').hitTestable());

    expect(find.text('Enter an HTTP or HTTPS URL.'), findsOneWidget);
    verifyNever(() => settings.changeServerSettings(any()));
  });

  testWidgets('copies the redirect URI and keeps the password login without OIDC', (tester) async {
    final copied = <String>[];
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (call) async {
      if (call.method == 'Clipboard.setData') {
        copied.add((call.arguments as Map<Object?, Object?>)['text'] as String);
      }
      return null;
    });
    addTearDown(() => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, null));
    await pumpSettings(tester);
    await expand(tester, 'Single sign-on (OIDC)');

    expect(tester.widget<Switch>(toggle('Allow the password login')).onChanged, isNull);
    await tapVisible(tester, button('Copy').first);

    expect(copied, ['http://localhost/api/auth/oidc/callback']);
    expect(button('Copied'), findsOneWidget);
    await tester.pump(const Duration(seconds: 3));
    await tester.pumpAndSettle();
    expect(button('Copied'), findsNothing);
  });

  testWidgets('saves the OIDC client and disables the password login', (tester) async {
    when(() => settings.changeOidcSettings(any())).thenAnswer((_) async => oidc);
    await pumpSettings(tester);
    await expand(tester, 'Single sign-on (OIDC)');

    await tester.enterText(field('issuer_url'), 'https://id.example.com');
    await tester.enterText(field('client_id'), 'guardou');
    await tester.enterText(field('client_secret'), 'secret');
    await tapVisible(tester, toggle('Allow the password login'));
    await tapVisible(tester, button('Save').hitTestable());

    verify(
      () => settings.changeOidcSettings(
        const OidcSettingsChange(
          issuer: 'https://id.example.com',
          clientId: 'guardou',
          clientSecret: 'secret',
          scopes: ['openid', 'profile', 'email'],
          userClaim: 'preferred_username',
          autoCreateUsers: true,
          passwordLogin: false,
        ),
      ),
    ).called(1);
  });

  testWidgets('requires the client ID with the issuer', (tester) async {
    await pumpSettings(tester);
    await expand(tester, 'Single sign-on (OIDC)');

    await tester.enterText(field('issuer_url'), 'https://id.example.com');
    await tapVisible(tester, button('Save').hitTestable());

    expect(find.text('Set the issuer URL and the client ID together.'), findsNWidgets(2));
    verifyNever(() => settings.changeOidcSettings(any()));
  });

  testWidgets('requires an HTTPS issuer', (tester) async {
    await pumpSettings(tester);
    await expand(tester, 'Single sign-on (OIDC)');

    await tester.enterText(field('issuer_url'), 'http://id.example.com');
    await tester.enterText(field('client_id'), 'guardou');
    await tapVisible(tester, button('Save').hitTestable());

    expect(find.text('Enter an HTTPS URL.'), findsOneWidget);
    verifyNever(() => settings.changeOidcSettings(any()));
  });

  testWidgets('works in portrait', (tester) async {
    await pumpSettings(tester, size: compact);
    await expand(tester, 'Single sign-on (OIDC)');

    expect(field('issuer_url'), findsOneWidget);
    await reveal(tester, button('Save').hitTestable());
    expect(tester.takeException(), isNull);
  });
}
