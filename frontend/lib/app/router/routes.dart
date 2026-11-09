abstract final class Routes {
  static const root = '/';
  static const loading = '/loading';
  static const login = '/login';
  static const register = '/register';
  static const suggested = '/suggested';
  static const files = '/files';
  static const folders = '/folders';
  static const file = '/file';
  static const view = '/view';
  static const sharedLink = '/s';
  static const starred = '/starred';
  static const shared = '/shared';
  static const trash = '/trash';
  static const accountSettings = '/settings';
  static const admin = '/admin';
  static const adminUsers = '/admin/users';
  static const adminTasks = '/admin/tasks';
  static const adminSettings = '/admin/settings';
  static const forbidden = '/403';
  static const serverError = '/500';

  static const redirectParameter = 'redirect';
  static const oidcErrorParameter = 'oidcError';
  static const idParameter = 'id';
  static const tokenParameter = 'token';
  static const rootFolderId = 'root';

  static const entryPages = {loading, login, register};
  static const errorPages = {forbidden, serverError};

  static String folder(String id) => id == rootFolderId ? files : '$folders/${Uri.encodeComponent(id)}';

  static String fileItem(String id) => '$file/${Uri.encodeComponent(id)}';

  static String viewer(String id) => '$view/${Uri.encodeComponent(id)}';

  static String sharedRoot(String token) => '$sharedLink/${Uri.encodeComponent(token)}';

  static String sharedFolder(String token, String id) => '${sharedRoot(token)}$folders/${Uri.encodeComponent(id)}';

  static String sharedViewer(String token, String id) => '${sharedRoot(token)}$view/${Uri.encodeComponent(id)}';

  static bool isPublic(String path) => path.startsWith('$sharedLink/');

  static String? folderIdOf(String location) {
    final segments = Uri.parse(location).pathSegments.where((segment) => segment.isNotEmpty).toList();
    return switch (segments) {
      [final first] when '/$first' == files => rootFolderId,
      [final first, final id] when '/$first' == folders => id,
      _ => null,
    };
  }
}
