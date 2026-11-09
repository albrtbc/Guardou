import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_details.dart';
import '../../core/layout/window_size_class.dart';
import '../../core/widgets/page_header.dart';
import '../../l10n/generated/app_localizations.dart';
import 'details_panel_controller.dart';
import 'folder_breadcrumbs.dart';
import 'folder_layouts.dart';

class FolderHeader extends StatelessWidget {
  const FolderHeader({super.key, required this.folder});

  final ItemDetails? folder;

  @override
  Widget build(BuildContext context) {
    final current = folder;
    return ItemsHeader(
      title: current == null
          ? const SizedBox.shrink()
          : FolderBreadcrumbs(folder: current, acceptsDrops: !current.shared, inSharedWithMe: current.shared),
      layoutKey: current?.entry.id,
      readOnly: current != null && !current.entry.permissions.canAddChildren,
    );
  }
}

class ItemsHeader extends ConsumerWidget {
  const ItemsHeader({super.key, required this.title, required this.layoutKey, this.readOnly = false});

  final Widget title;
  final String? layoutKey;
  final bool readOnly;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;
    final key = layoutKey;

    return PageHeader(
      child: Row(
        children: [
          Expanded(child: title),
          if (readOnly)
            Tooltip(
              message: l10n.readOnlyFolder,
              child: Icon(
                FluentIcons.lock_closed_24_regular,
                size: 18,
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
          if (key != null) FolderLayoutToggle(folderId: key),
          if (!compact)
            IconButton(
              tooltip: l10n.actionDetails,
              isSelected: ref.watch(detailsPanelProvider),
              onPressed: ref.read(detailsPanelProvider.notifier).toggle,
              icon: const Icon(FluentIcons.info_24_regular),
            ),
        ],
      ),
    );
  }
}

class FolderLayoutToggle extends ConsumerWidget {
  const FolderLayoutToggle({super.key, required this.folderId});

  final String folderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final layout = ref.watch(folderLayoutsProvider.select((layouts) => layouts.of(folderId)));
    return IconButton(
      tooltip: layout == FileLayout.list ? l10n.viewAsGrid : l10n.viewAsList,
      onPressed: () => ref.read(folderLayoutsProvider.notifier).toggle(folderId),
      icon: Icon(layout == FileLayout.list ? FluentIcons.grid_24_regular : FluentIcons.text_bullet_list_ltr_24_regular),
    );
  }
}
