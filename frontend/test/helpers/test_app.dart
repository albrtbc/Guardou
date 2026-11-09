import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/authentication_method.dart';
import 'package:guardou/api/generated/models/breadcrumb.dart';
import 'package:guardou/api/generated/models/current_user.dart';
import 'package:guardou/api/generated/models/item_creator.dart';
import 'package:guardou/api/generated/models/item_details.dart';
import 'package:guardou/api/generated/models/item_entry.dart';
import 'package:guardou/api/generated/models/item_kind.dart';
import 'package:guardou/api/generated/models/item_type.dart';
import 'package:guardou/api/generated/models/language.dart';
import 'package:guardou/api/generated/models/item_permissions.dart';
import 'package:guardou/api/generated/models/password_policy.dart';
import 'package:guardou/api/generated/models/runtime_configuration.dart';
import 'package:guardou/api/generated/models/storage_usage.dart';
import 'package:guardou/api/generated/models/start_page.dart';
import 'package:guardou/api/generated/models/theme_preference.dart';
import 'package:guardou/api/generated/models/upload_conflict_policy.dart';
import 'package:guardou/api/generated/models/user_preferences.dart';
import 'package:guardou/app/app.dart';
import 'package:guardou/app/router/app_router.dart';
import 'package:guardou/app/router/routes.dart';
import 'package:guardou/app/theme/app_theme.dart';
import 'package:guardou/core/api/api_problem.dart';
import 'package:guardou/core/browser/browser_embeds.dart';
import 'package:guardou/core/browser/browser_files.dart';
import 'package:guardou/core/browser/browser_location.dart';
import 'package:guardou/features/auth/auth_service.dart';
import 'package:guardou/features/configuration/configuration_service.dart';
import 'package:guardou/features/files/files_service.dart';
import 'package:guardou/features/preferences/preferences_service.dart';
import 'package:guardou/features/profile/profile_picture_service.dart';
import 'package:guardou/features/settings/settings_service.dart';
import 'package:guardou/features/shares/shares_service.dart';
import 'package:guardou/features/tasks/tasks_service.dart';
import 'package:guardou/features/trash/trash_service.dart';
import 'package:guardou/features/users/users_service.dart';
import 'package:guardou/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class MockAuthService extends Mock implements AuthService {}

class MockConfigurationService extends Mock implements ConfigurationService {}

class MockFilesService extends Mock implements FilesService {}

class MockTrashService extends Mock implements TrashService {}

class MockBrowserFiles extends Mock implements BrowserFiles {}

class MockBrowserLocation extends Mock implements BrowserLocation {}

class MockUsersService extends Mock implements UsersService {}

class MockTasksService extends Mock implements TasksService {}

class MockSettingsService extends Mock implements SettingsService {}

class MockSharesService extends Mock implements SharesService {}

class MockProfilePictureService extends Mock implements ProfilePictureService {}

class MockPreferencesService extends Mock implements PreferencesService {}

class FakeBrowserEmbeds implements BrowserEmbeds {
  @override
  Widget video(Uri source) => Text('video $source');

  @override
  Widget audio(Uri source) => Text('audio $source');

  @override
  Widget document(Uri source) => Text('document $source');
}

const testConfiguration = RuntimeConfiguration(
  applicationName: 'Guardou',
  authenticationMethods: [AuthenticationMethod.password],
  registrationOpen: false,
  passwordPolicy: PasswordPolicy(
    minimumLength: 12,
    requireDigit: false,
    requireLowercase: false,
    requireUppercase: false,
    requireNonAlphanumeric: false,
  ),
  trashRetentionDays: 30,
  uploadConcurrency: 3,
  uploadChunkSizeMb: 64,
);

const testUser = CurrentUser(
  id: 'admin-id',
  username: 'admin',
  displayName: 'Admin User',
  email: 'admin@example.com',
  isAdmin: true,
  hasPicture: false,
);

const testPreferences = UserPreferences(
  theme: ThemePreference.system,
  language: Language.system,
  startPage: StartPage.files,
  uploadConflictPolicy: UploadConflictPolicy.ask,
);

MockPreferencesService stubPreferences({UserPreferences preferences = testPreferences}) {
  final service = MockPreferencesService();
  when(service.preferences).thenAnswer((_) async => preferences);
  when(service.defaults).thenAnswer((_) async => testPreferences);
  return service;
}

const testUsage = StorageUsage(usedBytes: 1024 * 1024, totalBytes: 4 * 1024 * 1024, quotaBytes: null);

const testRootId = 'root-id';

ItemEntry testEntry(
  String name, {
  String? id,
  String? parentId = testRootId,
  ItemKind kind = ItemKind.file,
  ItemType? type,
  int size = 0,
  DateTime? modified,
  DateTime? opened,
  bool starred = false,
  bool sharedByMe = false,
  bool canModify = true,
  bool hasPreview = false,
  String mimeType = 'text/plain',
}) {
  final date = modified ?? DateTime.utc(2026, 1, 1);
  return ItemEntry(
    id: id ?? name,
    parentId: parentId,
    name: name,
    kind: kind,
    type: type ?? (kind == ItemKind.folder ? ItemType.folder : ItemType.text),
    size: size,
    mimeType: kind == ItemKind.folder ? null : mimeType,
    modified: date,
    created: date,
    opened: opened,
    starred: starred,
    sharedByMe: sharedByMe,
    creator: ItemCreator(id: testUser.id, displayName: testUser.displayName, hasPicture: testUser.hasPicture),
    hasPreview: hasPreview,
    permissions: ItemPermissions(canModify: canModify, canAddChildren: kind == ItemKind.folder),
  );
}

final testRoot = testEntry('', id: testRootId, parentId: null, kind: ItemKind.folder, canModify: false);

const testRootCrumb = Breadcrumb(id: testRootId, name: '');

ItemDetails testFolder(
  ItemEntry entry,
  List<ItemEntry> items, {
  List<Breadcrumb> ancestors = const [],
  bool shared = false,
}) => ItemDetails(entry: entry, ancestors: ancestors, items: items, content: null, shared: shared);

MockFilesService stubFiles({Map<String, ItemDetails>? folders}) {
  final known = folders ?? {Routes.rootFolderId: testFolder(testRoot, const [])};
  final files = MockFilesService();
  when(files.usage).thenAnswer((_) async => testUsage);
  when(() => files.item(any())).thenAnswer((invocation) async {
    final id = invocation.positionalArguments.single as String;
    return known[id] ?? (throw testNotFound(id));
  });
  when(() => files.children(any())).thenAnswer((invocation) async {
    final id = invocation.positionalArguments.single as String;
    return [...?known[id]?.items];
  });
  when(() => files.recordOpen(any())).thenAnswer((_) async {});
  when(files.recent).thenAnswer((_) async => []);
  when(files.starred).thenAnswer((_) async => []);
  return files;
}

MockBrowserFiles stubBrowser() {
  final browser = MockBrowserFiles();
  when(() => browser.dragEvents).thenAnswer((_) => const Stream.empty());
  return browser;
}

DioException testNotFound(String path) => testProblem(path, status: 404);

DioException testProblem(String path, {required int status, List<String> codes = const []}) {
  final options = RequestOptions(path: path);
  return DioException(
    requestOptions: options,
    response: Response<Object?>(requestOptions: options, statusCode: status),
    error: ApiProblem(status: status, codes: codes),
  );
}

List<Override> serviceOverrides({
  required AuthService auth,
  CurrentUser? user = testUser,
  RuntimeConfiguration configuration = testConfiguration,
  FilesService? files,
  TrashService? trash,
  UsersService? users,
  TasksService? tasks,
  SettingsService? settings,
  SharesService? shares,
  ProfilePictureService? pictures,
  PreferencesService? preferences,
  BrowserFiles? browser,
  BrowserEmbeds? embeds,
  BrowserLocation? location,
}) {
  final configurationService = MockConfigurationService();
  when(configurationService.load).thenAnswer((_) async => configuration);
  when(auth.restore).thenAnswer((_) async => user);
  return [
    configurationServiceProvider.overrideWithValue(configurationService),
    authServiceProvider.overrideWithValue(auth),
    filesServiceProvider.overrideWithValue(files ?? stubFiles()),
    trashServiceProvider.overrideWithValue(trash ?? MockTrashService()),
    usersServiceProvider.overrideWithValue(users ?? MockUsersService()),
    tasksServiceProvider.overrideWithValue(tasks ?? MockTasksService()),
    settingsServiceProvider.overrideWithValue(settings ?? MockSettingsService()),
    sharesServiceProvider.overrideWithValue(shares ?? MockSharesService()),
    profilePictureServiceProvider.overrideWithValue(pictures ?? MockProfilePictureService()),
    preferencesServiceProvider.overrideWithValue(preferences ?? stubPreferences()),
    browserFilesProvider.overrideWithValue(browser ?? stubBrowser()),
    browserEmbedsProvider.overrideWithValue(embeds ?? FakeBrowserEmbeds()),
    browserLocationProvider.overrideWithValue(location ?? MockBrowserLocation()),
  ];
}

Future<void> pumpTestApp(
  WidgetTester tester, {
  required Widget child,
  required List<Override> overrides,
  Size size = const Size(1280, 800),
  Locale? locale,
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  await tester.pumpWidget(
    ProviderScope(
      overrides: overrides,
      child: MaterialApp(
        locale: locale,
        theme: AppTheme.light(),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: child,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<ProviderContainer> pumpRoutedApp(
  WidgetTester tester, {
  required String location,
  required List<Override> overrides,
  Size size = const Size(1280, 800),
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  final container = ProviderContainer(overrides: overrides);
  addTearDown(container.dispose);
  await tester.pumpWidget(UncontrolledProviderScope(container: container, child: const App()));
  await tester.pumpAndSettle();
  container.read(appRouterProvider).go(location);
  await tester.pumpAndSettle();
  return container;
}
