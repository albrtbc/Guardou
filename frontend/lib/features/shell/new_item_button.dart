import 'dart:async';

import 'package:fluentui_system_icons/fluentui_system_icons.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/generated/app_localizations.dart';
import '../files/file_actions.dart';
import '../files/folder_controller.dart';
import '../files/item_actions_menu.dart';
import '../files/new_item_menu.dart';

enum NewItemButtonStyle { floating, small, extended }

class NewItemButton extends ConsumerWidget {
  const NewItemButton({super.key, required this.style, required this.folderId});

  final NewItemButtonStyle style;
  final String folderId;

  void _create(BuildContext context, NewItemKind kind) => unawaited(kind.create(FileActions(context), folderId));

  Future<void> _showSheet(BuildContext context) async {
    final l10n = AppLocalizations.of(context);
    final kind = await showModalBottomSheet<NewItemKind>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            for (final kind in NewItemKind.values)
              ListTile(
                leading: Icon(kind.icon),
                title: Text(kind.label(l10n)),
                onTap: () => Navigator.of(context).pop(kind),
              ),
          ],
        ),
      ),
    );
    if (kind != null && context.mounted) {
      _create(context, kind);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final canAdd = ref.watch(
      folderControllerProvider(folderId).select((folder) => folder.value?.entry.permissions.canAddChildren ?? false),
    );
    const icon = Icon(FluentIcons.add_24_filled);

    if (style == NewItemButtonStyle.floating) {
      return FloatingActionButton(
        heroTag: null,
        tooltip: l10n.newItem,
        onPressed: canAdd ? () => unawaited(_showSheet(context)) : null,
        child: icon,
      );
    }
    return Builder(
      builder: (anchor) {
        final VoidCallback? open = canAdd
            ? () => unawaited(showNewItemMenu(anchor, folderId: folderId, position: anchorPosition(anchor)))
            : null;
        return switch (style) {
          NewItemButtonStyle.extended => FloatingActionButton.extended(
            heroTag: null,
            elevation: 1,
            onPressed: open,
            icon: icon,
            label: Text(l10n.newItem),
          ),
          _ => FloatingActionButton(heroTag: null, tooltip: l10n.newItem, elevation: 0, onPressed: open, child: icon),
        };
      },
    );
  }
}
