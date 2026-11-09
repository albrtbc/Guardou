import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'browser_location_stub.dart' if (dart.library.js_interop) 'browser_location_web.dart';

abstract interface class BrowserLocation {
  void assign(Uri uri);

  void open(Uri uri);
}

final browserLocationProvider = Provider<BrowserLocation>((ref) => createBrowserLocation());
