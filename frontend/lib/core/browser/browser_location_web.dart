import 'package:web/web.dart' as web;

import 'browser_location.dart';

BrowserLocation createBrowserLocation() => const _WebBrowserLocation();

class _WebBrowserLocation implements BrowserLocation {
  const _WebBrowserLocation();

  @override
  void assign(Uri uri) => web.window.location.assign(uri.toString());

  @override
  void open(Uri uri) => web.window.open(uri.toString(), '_blank', 'noopener,noreferrer');
}
