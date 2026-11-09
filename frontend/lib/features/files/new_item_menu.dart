import 'dart:async';

import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../l10n/generated/app_localizations.dart';
import 'file_actions.dart';
import 'item_actions_menu.dart';

enum NewItemKind {
  folder(FluentIcons.folder_add_24_regular),
  fileUpload(FluentIcons.document_arrow_up_20_regular),
  folderUpload(FluentIcons.folder_arrow_up_24_regular);

  const NewItemKind(this.icon);

  final IconData icon;

  String label(AppLocalizations l10n) => switch (this) {
    folder => l10n.newFolder,
    fileUpload => l10n.uploadFiles,
    folderUpload => l10n.uploadFolder,
  };

  Future<void> create(FileActions actions, String folderId) => switch (this) {
    folder => actions.createFolder(folderId),
    fileUpload => actions.uploadFiles(folderId),
    folderUpload => actions.uploadFolder(folderId),
  };
}

Future<void> showNewItemMenu(BuildContext context, {required String folderId, required Offset position}) async {
  final l10n = AppLocalizations.of(context);
  final kind = await showMenu<NewItemKind>(
    context: context,
    position: menuPositionAt(context, position),
    items: [
      for (final kind in NewItemKind.values)
        PopupMenuItem(
          value: kind,
          child: MenuItemLabel(icon: kind.icon, label: kind.label(l10n)),
        ),
    ],
  );
  if (kind != null && context.mounted) {
    await kind.create(FileActions(context), folderId);
  }
}
