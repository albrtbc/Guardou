import 'package:flutter_test/flutter_test.dart';
import 'package:guardou/app/router/routes.dart';

void main() {
  test('builds folder routes', () {
    expect(Routes.folder(Routes.rootFolderId), '/files');
    expect(Routes.folder('AbC-_x'), '/folders/AbC-_x');
  });

  test('reads the folder id from a location', () {
    expect(Routes.folderIdOf('/files'), Routes.rootFolderId);
    expect(Routes.folderIdOf('/files/'), Routes.rootFolderId);
    expect(Routes.folderIdOf('/folders/AbC'), 'AbC');
    expect(Routes.folderIdOf('/file/AbC'), isNull);
    expect(Routes.folderIdOf('/suggested'), isNull);
  });
}
