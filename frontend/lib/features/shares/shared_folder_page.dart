import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_details.dart';
import '../../api/generated/models/item_entry.dart';
import '../../api/generated/models/item_kind.dart';
import '../../app/router/routes.dart';
import '../../core/browser/browser_files.dart';
import '../../core/widgets/page_header.dart';
import '../../core/widgets/source_code_link.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/drag_select_area.dart';
import '../files/file_arrangement.dart';
import '../files/file_grid_view.dart';
import '../files/file_list_view.dart';
import '../files/file_source.dart';
import '../files/file_view_settings.dart';
import '../files/folder_breadcrumbs.dart';
import '../files/folder_header.dart';
import '../files/folder_layouts.dart';
import '../files/folder_states.dart';
import '../files/item_action.dart';
import '../files/item_actions_menu.dart';
import '../files/item_interactions.dart';
import '../files/selection.dart';
import 'shared_items.dart';
import 'shared_join_button.dart';
import 'shared_link_states.dart';
import 'shares_service.dart';

class SharedFolderPage extends ConsumerWidget {
  const SharedFolderPage({super.key, required this.token, required this.folderId});

  final String token;
  final String folderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final key = (token: token, id: folderId);
    return switch (ref.watch(sharedDetailsProvider(key))) {
      AsyncValue(hasValue: true, :final value?) => _SharedFolder(token: token, folder: value),
      AsyncValue(:final error?) => SharedLinkError(
        error: error,
        onRetry: () => ref.invalidate(sharedDetailsProvider(key)),
      ),
      _ => const SharedLoadingPage(),
    };
  }
}

class _SharedFolder extends ConsumerStatefulWidget {
  const _SharedFolder({required this.token, required this.folder});

  static const _menuActions = [ItemAction.open, ItemAction.download];

  final String token;
  final ItemDetails folder;

  @override
  ConsumerState<_SharedFolder> createState() => _SharedFolderState();
}

class _SharedFolderState extends ConsumerState<_SharedFolder> {
  final _keys = ItemKeyRegistry();

  String get _token => widget.token;

  FileSource get _source => ref.read(sharesServiceProvider).files(_token);

  void _open(ItemEntry entry) => context.go(
    entry.kind == ItemKind.folder ? Routes.sharedFolder(_token, entry.id) : Routes.sharedViewer(_token, entry.id),
  );

  void _download(ItemEntry entry) => ref.read(browserFilesProvider).download(_source.downloadUri(entry.id));

  Future<void> _showActions(ItemEntry entry, Offset position) async {
    final action = await showItemActions(
      context,
      entries: [entry],
      position: position,
      actions: _SharedFolder._menuActions,
    );
    switch (action) {
      case ItemAction.open:
        _open(entry);
      case ItemAction.download:
        _download(entry);
      case _:
        return;
    }
  }

  ItemInteractions _interactions() => ItemInteractions(
    selection: Selection.empty,
    openOnTap: true,
    onTap: _open,
    onOpen: _open,
    onLongPress: (_) {},
    onContextMenu: (entry, position) => unawaited(_showActions(entry, position)),
    onMore: (entry, anchor) => unawaited(_showActions(entry, anchorPosition(anchor))),
    keyFor: _keys.keyFor,
    decorate: (_, child) => child,
    source: _source,
  );

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final folder = widget.folder;
    final entries = arrangeEntries(folder.items ?? const [], ref.watch(fileViewSettingsProvider));
    final layout = ref.watch(folderLayoutsProvider.select((layouts) => layouts.of(folder.entry.id)));
    final scrollKey = '$_token:${folder.entry.id}:${layout.name}';
    final interactions = _interactions();

    return Scaffold(
      appBar: AppBar(
        leading: const SharedBrand(),
        title: Text(folder.ancestors.firstOrNull?.name ?? folder.entry.name, overflow: TextOverflow.ellipsis),
        actions: [
          SharedJoinButton(token: _token, itemId: folder.entry.id, location: Routes.folder(folder.entry.id)),
          IconButton(
            tooltip: l10n.actionDownload,
            onPressed: () => _download(folder.entry),
            icon: const Icon(FluentIcons.arrow_download_24_regular),
          ),
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          PageHeader(
            child: Row(
              children: [
                Expanded(
                  child: FolderBreadcrumbs(
                    folder: folder,
                    routeOf: (crumb) => Routes.sharedFolder(_token, crumb.id),
                    acceptsDrops: false,
                  ),
                ),
                FolderLayoutToggle(folderId: folder.entry.id),
              ],
            ),
          ),
          Expanded(
            child: switch ((entries.isEmpty, layout)) {
              (true, _) => const EmptyFolderView(),
              (false, FileLayout.list) => FileListView(
                entries: entries,
                interactions: interactions,
                scrollKey: scrollKey,
              ),
              (false, FileLayout.grid) => FileGridView(
                entries: entries,
                interactions: interactions,
                scrollKey: scrollKey,
              ),
            },
          ),
          const SourceCodeLink(),
        ],
      ),
    );
  }
}
