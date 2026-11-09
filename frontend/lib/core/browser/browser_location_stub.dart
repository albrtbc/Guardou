import 'browser_location.dart';

BrowserLocation createBrowserLocation() => const _UnsupportedBrowserLocation();

class _UnsupportedBrowserLocation implements BrowserLocation {
  const _UnsupportedBrowserLocation();

  @override
  void assign(Uri uri) {}

  @override
  void open(Uri uri) {}
}
