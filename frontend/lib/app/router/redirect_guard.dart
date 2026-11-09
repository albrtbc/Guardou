import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'routes.dart';

String? safeRedirect(String? value) {
  if (value == null || !value.startsWith('/') || value.startsWith('//') || value.contains(r'\')) {
    return null;
  }
  final uri = Uri.tryParse(value);
  if (uri == null || uri.hasScheme || uri.hasAuthority || Routes.entryPages.contains(uri.path)) {
    return null;
  }
  return value;
}

String? resolveRedirect({
  required AsyncValue<Object?> configuration,
  required AsyncValue<Object?> session,
  required bool registrationOpen,
  required Uri location,
}) {
  final path = location.path;
  if (Routes.isPublic(path)) {
    return null;
  }

  final target = _targetOf(location);
  if (configuration.hasError || session.hasError) {
    return path == Routes.serverError ? null : Routes.serverError;
  }
  if (!configuration.hasValue || !session.hasValue) {
    return path == Routes.loading ? null : _withRedirect(Routes.loading, target);
  }
  if (session.value == null) {
    final signInPage = registrationOpen ? Routes.register : Routes.login;
    if (path == signInPage || Routes.errorPages.contains(path)) {
      return null;
    }
    return _withRedirect(signInPage, target);
  }
  if (Routes.entryPages.contains(path)) {
    return target ?? Routes.root;
  }
  return null;
}

String? _targetOf(Uri location) {
  if (Routes.entryPages.contains(location.path)) {
    return safeRedirect(location.queryParameters[Routes.redirectParameter]);
  }
  if (Routes.errorPages.contains(location.path)) {
    return null;
  }
  return safeRedirect(location.toString());
}

String _withRedirect(String path, String? target) {
  if (target == null || target == Routes.root) {
    return path;
  }
  return Uri(path: path, queryParameters: {Routes.redirectParameter: target}).toString();
}
