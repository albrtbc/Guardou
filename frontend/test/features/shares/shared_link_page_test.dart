import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/api/generated/models/breadcrumb.dart';
import 'package:guardou/api/generated/models/item_creator.dart';
import 'package:guardou/api/generated/models/item_kind.dart';
import 'package:guardou/app/router/app_router.dart';
import 'package:guardou/core/widgets/source_code_link.dart';
import 'package:guardou/features/shares/shared_file_source.dart';
import 'package:mocktail/mocktail.dart';

import '../../helpers/test_app.dart';

void main() {
  const token = 'AAAAAAAAAAAAAAAAAAAAAA';
  const portrait = Size(390, 844);
  final clip = testEntry('clip.mp4', id: 'clip-id', parentId: null, canModify: false, mimeType: 'video/mp4');
  final docs = testEntry('docs', id: 'docs-id', parentId: null, kind: ItemKind.folder, canModify: false);
  final inner = testEntry('inner', id: 'inner-id', parentId: docs.id, kind: ItemKind.folder, canModify: false);
  final first = testEntry('a-photo.mp4', id: 'first-id', parentId: inner.id, canModify: false, mimeType: 'video/mp4');
  final second = testEntry(
    'b-song.mp3',
    id: 'second-id',
    parentId: inner.id,
    canModify: false,
    mimeType: 'audio/mpeg',
  ).copyWith(creator: const ItemCreator(id: null, displayName: 'Zoe Owner', hasPicture: false));
  const docsCrumb = Breadcrumb(id: 'docs-id', name: 'docs');
  const innerCrumb = Breadcrumb(id: 'inner-id', name: 'inner');

  late MockSharesService shares;
  late MockBrowserFiles browser;
  var member = false;

  setUp(() {
    member = false;
    shares = MockSharesService();
    browser = stubBrowser();
    when(() => shares.files(token)).thenReturn(SharedFileSource(Dio(), Uri.parse('http://localhost'), token));
    final known = {
      clip.id: testFolder(clip, const []),
      docs.id: testFolder(docs, [inner]),
      inner.id: testFolder(inner, [first, second], ancestors: const [docsCrumb]),
      first.id: testFolder(first, const [], ancestors: const [docsCrumb, innerCrumb]),
    };
    when(() => shares.sharedDetails(token, any())).thenAnswer((invocation) async {
      final id = invocation.positionalArguments.last as String;
      return known[id] ?? (throw testNotFound(id));
    });
  });

  Future<ProviderContainer> pumpShare(WidgetTester tester, String location, {Size size = const Size(1280, 800)}) =>
      pumpRoutedApp(
        tester,
        location: location,
        size: size,
        overrides: serviceOverrides(auth: MockAuthService(), user: null, shares: shares, browser: browser),
      );

  testWidgets('shows a shared file without a session', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => clip);

    await pumpShare(tester, '/s/$token');

    expect(find.text(clip.name), findsOneWidget);
    expect(find.text('video http://localhost/api/shares/$token/items/${clip.id}/view'), findsOneWidget);
    expect(find.byTooltip('Close'), findsNothing);
  });

  testWidgets('lists a shared folder and opens a subfolder in portrait', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);

    await pumpShare(tester, '/s/$token', size: portrait);
    await tester.tap(find.text('inner'));
    await tester.pumpAndSettle();

    expect(find.text(first.name), findsOneWidget);
    expect(find.text(second.name), findsOneWidget);
    expect(find.text('docs'), findsWidgets);
  });

  testWidgets('shows the creator of each file, not the viewer', (tester) async {
    await pumpShare(tester, '/s/$token/folders/${inner.id}');
    await tester.tap(find.byTooltip('Show as grid'));
    await tester.pumpAndSettle();

    expect(find.text('Z'), findsOneWidget);
    expect(find.text('A'), findsOneWidget);
    expect(find.textContaining('Zoe Owner created'), findsOneWidget);
    expect(find.textContaining('You created'), findsNothing);
  });

  testWidgets('steps through the media files of a shared folder', (tester) async {
    final container = await pumpShare(tester, '/s/$token/view/${first.id}');

    await tester.tap(find.byTooltip('Next file'));
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).routeInformationProvider.value.uri.path, '/s/$token/view/${second.id}');
  });

  testWidgets('closes the viewer to the shared folder', (tester) async {
    final container = await pumpShare(tester, '/s/$token/view/${first.id}');

    await tester.tap(find.byTooltip('Close'));
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).routeInformationProvider.value.uri.path, '/s/$token/folders/${inner.id}');
  });

  testWidgets('downloads the shared folder', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);

    await pumpShare(tester, '/s/$token');
    await tester.tap(find.byTooltip('Download'));

    verify(() => browser.download(Uri.parse('http://localhost/api/shares/$token/items/${docs.id}/download'))).called(1);
  });

  testWidgets('tells that an unknown link is not available', (tester) async {
    when(() => shares.sharedItem(token)).thenThrow(testNotFound('/api/shares/$token'));

    await pumpShare(tester, '/s/$token');

    expect(find.text('This link does not exist, or the owner stopped sharing it.'), findsOneWidget);
    expect(find.text('Source code'), findsOneWidget);
  });

  Future<ProviderContainer> pumpSignedIn(WidgetTester tester, String location, {Size size = const Size(1280, 800)}) {
    final files = stubFiles(
      folders: {
        docs.id: testFolder(docs, [inner], shared: true),
        first.id: testFolder(first, const [], ancestors: const [docsCrumb, innerCrumb], shared: true),
      },
    );
    when(() => files.viewUri(any())).thenAnswer((invocation) => Uri.parse('/view/${invocation.positionalArguments.single}'));
    final reachable = {docs.id: docs, first.id: first, clip.id: clip};
    when(() => files.item(any())).thenAnswer((invocation) async {
      final id = invocation.positionalArguments.single as String;
      final entry = reachable[id];
      return member && entry != null ? testFolder(entry, const []) : throw testNotFound(id);
    });
    return pumpRoutedApp(
      tester,
      location: location,
      size: size,
      overrides: serviceOverrides(auth: MockAuthService(), shares: shares, browser: browser, files: files),
    );
  }

  testWidgets('does not join a shared folder link when a signed-in user opens it', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);

    final container = await pumpSignedIn(tester, '/s/$token');

    verifyNever(() => shares.join(any()));
    expect(container.read(appRouterProvider).state.uri.path, '/s/$token');
    expect(find.text('inner'), findsOneWidget);
    expect(find.text('Add to Shared with me'), findsOneWidget);
  });

  testWidgets('does not join a shared file link when a signed-in user opens it', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => clip);

    final container = await pumpSignedIn(tester, '/s/$token/view/${clip.id}');

    verifyNever(() => shares.join(any()));
    expect(container.read(appRouterProvider).state.uri.path, '/s/$token/view/${clip.id}');
    expect(find.text('Add to Shared with me'), findsOneWidget);
  });

  testWidgets('joins a shared folder as a direct share when the button is pressed', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);
    when(() => shares.join(token)).thenAnswer((_) async {
      member = true;
      return docs;
    });
    final container = await pumpSignedIn(tester, '/s/$token');

    await tester.tap(find.text('Add to Shared with me'));
    await tester.pumpAndSettle();

    verify(() => shares.join(token)).called(1);
    expect(container.read(appRouterProvider).state.uri.path, '/folders/${docs.id}');
    expect(find.text('Shared with me'), findsWidgets);
  });

  testWidgets('joins from a file inside the link and opens it in the own viewer', (tester) async {
    when(() => shares.join(token)).thenAnswer((_) async {
      member = true;
      return docs;
    });
    final container = await pumpSignedIn(tester, '/s/$token/view/${first.id}');

    await tester.tap(find.text('Add to Shared with me'));
    await tester.pumpAndSettle();

    verify(() => shares.join(token)).called(1);
    expect(container.read(appRouterProvider).state.uri.path, '/view/${first.id}');
  });

  testWidgets('shows the join button as an icon in portrait', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);
    when(() => shares.join(token)).thenAnswer((_) async {
      member = true;
      return docs;
    });
    final container = await pumpSignedIn(tester, '/s/$token', size: portrait);

    expect(find.text('Add to Shared with me'), findsNothing);
    await tester.tap(find.byTooltip('Add to Shared with me'));
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).state.uri.path, '/folders/${docs.id}');
  });

  testWidgets('stays on the public page and tells when the join fails', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);
    when(() => shares.join(token)).thenThrow(testNotFound(token));
    final container = await pumpSignedIn(tester, '/s/$token');

    await tester.tap(find.text('Add to Shared with me'));
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).state.uri.path, '/s/$token');
    expect(find.text('Something went wrong. Try again.'), findsOneWidget);
    expect(find.text('Add to Shared with me'), findsOneWidget);
  });

  testWidgets('shows the unavailable link page to a signed-in user', (tester) async {
    when(() => shares.sharedItem(token)).thenThrow(testNotFound(token));

    await pumpSignedIn(tester, '/s/$token');

    verifyNever(() => shares.join(any()));
    expect(find.text('This link does not exist, or the owner stopped sharing it.'), findsOneWidget);
  });

  testWidgets('offers to open a shared folder that the user already reaches', (tester) async {
    member = true;
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);
    final container = await pumpSignedIn(tester, '/s/$token');

    expect(find.text('Add to Shared with me'), findsNothing);
    await tester.tap(find.widgetWithText(TextButton, 'Open'));
    await tester.pumpAndSettle();

    verifyNever(() => shares.join(any()));
    expect(container.read(appRouterProvider).state.uri.path, '/folders/${docs.id}');
  });

  testWidgets('offers to open a shared file that the user already reaches', (tester) async {
    member = true;
    final container = await pumpSignedIn(tester, '/s/$token/view/${first.id}');

    expect(find.text('Add to Shared with me'), findsNothing);
    await tester.tap(find.widgetWithText(TextButton, 'Open'));
    await tester.pumpAndSettle();

    expect(container.read(appRouterProvider).state.uri.path, '/view/${first.id}');
  });

  testWidgets('offers to join when the item is not found for the user', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);

    await pumpSignedIn(tester, '/s/$token');

    expect(find.text('Add to Shared with me'), findsOneWidget);
    expect(find.text('Open'), findsNothing);
  });

  testWidgets('offers to join when the access check fails for another reason', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);
    final files = stubFiles();
    when(() => files.item(any())).thenThrow(testProblem('/api/items/${docs.id}', status: 500));

    await pumpRoutedApp(
      tester,
      location: '/s/$token',
      overrides: serviceOverrides(auth: MockAuthService(), shares: shares, browser: browser, files: files),
    );

    expect(find.text('Add to Shared with me'), findsOneWidget);
    expect(find.text('Open'), findsNothing);
  });

  testWidgets('offers no join button without a session', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);

    await pumpShare(tester, '/s/$token');

    expect(find.text('Add to Shared with me'), findsNothing);
    expect(find.text('Open'), findsNothing);
  });

  testWidgets('links to the source code from a shared folder', (tester) async {
    final location = MockBrowserLocation();
    when(() => shares.sharedItem(token)).thenAnswer((_) async => docs);
    await pumpRoutedApp(
      tester,
      location: '/s/$token',
      overrides: serviceOverrides(
        auth: MockAuthService(),
        user: null,
        shares: shares,
        browser: browser,
        location: location,
      ),
    );

    await tester.tap(find.text('Source code'));

    verify(() => location.open(Uri.parse(sourceCodeUrl))).called(1);
  });

  testWidgets('links to the source code from a shared file', (tester) async {
    when(() => shares.sharedItem(token)).thenAnswer((_) async => clip);

    await pumpShare(tester, '/s/$token');

    expect(find.text('Source code'), findsOneWidget);
  });
}
