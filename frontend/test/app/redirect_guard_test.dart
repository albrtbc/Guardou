import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/app/router/redirect_guard.dart';

void main() {
  group('safeRedirect', () {
    test('accepts relative paths', () {
      expect(safeRedirect('/trash'), '/trash');
      expect(safeRedirect('/files?view=grid'), '/files?view=grid');
    });

    test('refuses other values', () {
      for (final value in [null, '', 'trash', '//evil.example', 'https://evil.example', r'/\evil.example', '/login']) {
        expect(safeRedirect(value), isNull, reason: '$value');
      }
    });
  });

  group('resolveRedirect', () {
    const loaded = AsyncData<Object?>('configuration');
    const signedIn = AsyncData<Object?>('user');
    const signedOut = AsyncData<Object?>(null);

    String? resolve(
      String location, {
      AsyncValue<Object?> configuration = loaded,
      AsyncValue<Object?> session = signedIn,
      bool registrationOpen = false,
    }) => resolveRedirect(
      configuration: configuration,
      session: session,
      registrationOpen: registrationOpen,
      location: Uri.parse(location),
    );

    test('opens shared links without a session', () {
      expect(resolve('/s/token', session: signedOut), isNull);
      expect(resolve('/s/token', session: const AsyncLoading()), isNull);
    });

    test('shows the loading page and keeps the target', () {
      expect(resolve('/trash', session: const AsyncLoading()), '/loading?redirect=%2Ftrash');
      expect(resolve('/loading', session: const AsyncLoading()), isNull);
    });

    test('sends a signed out user to the login page with the target', () {
      expect(resolve('/trash', session: signedOut), '/login?redirect=%2Ftrash');
      expect(resolve('/', session: signedOut), '/login');
      expect(resolve('/login', session: signedOut), isNull);
    });

    test('sends a signed out user to the registration while no user exists', () {
      expect(resolve('/trash', session: signedOut, registrationOpen: true), '/register?redirect=%2Ftrash');
      expect(resolve('/login', session: signedOut, registrationOpen: true), '/register');
      expect(resolve('/register', session: signedOut, registrationOpen: true), isNull);
      expect(resolve('/register', session: signedOut), '/login');
    });

    test('sends a signed in user to the target after login', () {
      expect(resolve('/register?redirect=%2Fstarred'), '/starred');
      expect(resolve('/login?redirect=%2Ftrash'), '/trash');
      expect(resolve('/login?redirect=https%3A%2F%2Fevil.example'), '/');
      expect(resolve('/loading'), '/');
    });

    test('shows the server error page when loading fails', () {
      expect(resolve('/suggested', configuration: AsyncError<Object?>(Exception(), StackTrace.empty)), '/500');
      expect(resolve('/500', configuration: AsyncError<Object?>(Exception(), StackTrace.empty)), isNull);
    });

    test('lets a signed in user stay on the page', () {
      expect(resolve('/starred'), isNull);
    });
  });
}
