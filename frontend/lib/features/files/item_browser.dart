import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';

import '../../api/generated/models/item_entry.dart';
import '../../api/generated/models/item_kind.dart';
import '../../core/layout/window_size_class.dart';
import '../../l10n/generated/app_localizations.dart';
import 'details_panel.dart';
import 'details_panel_controller.dart';
import 'drag_select_area.dart';
import 'file_actions.dart';
import 'file_arrangement.dart';
import 'file_drop_target.dart';
import 'file_grid_view.dart';
import 'file_list_view.dart';
import 'file_view_settings.dart';
import 'files_service.dart';
import 'folder_layouts.dart';
import 'item_action.dart';
import 'item_actions_menu.dart';
import 'item_drag.dart';
import 'item_interactions.dart';
import 'new_item_menu.dart';
import 'selection.dart';
import 'selection_controller.dart';
import 'selection_toolbar.dart';

class ItemBrowser extends ConsumerStatefulWidget {
  const ItemBrowser({
    super.key,
    required this.scope,
    required this.layoutKey,
    required this.items,
    required this.header,
    required this.empty,
    required this.errorView,
    this.folder,
    this.focusedItemId,
    this.menuActions = ItemAction.values,
    this.toolbarActions = defaultToolbarActions,
  });

  static const defaultToolbarActions = [
    ItemAction.download,
    ItemAction.share,
    ItemAction.star,
    ItemAction.unstar,
    ItemAction.move,
    ItemAction.copy,
    ItemAction.rename,
    ItemAction.trash,
    ItemAction.details,
  ];

  static const _panelWidth = 320.0;

  final String scope;
  final String layoutKey;
  final ProviderListenable<AsyncValue<List<ItemEntry>>> items;
  final Widget header;
  final Widget empty;
  final Widget Function(Object error) errorView;
  final ItemEntry? folder;
  final String? focusedItemId;
  final List<ItemAction> menuActions;
  final List<ItemAction> toolbarActions;

  @override
  ConsumerState<ItemBrowser> createState() => _ItemBrowserState();
}

class _ItemBrowserState extends ConsumerState<ItemBrowser> {
  final _keys = ItemKeyRegistry();
  late String? _pendingFocus = widget.focusedItemId;

  SelectionController get _selection => ref.read(selectionControllerProvider(widget.scope).notifier);

  @override
  void initState() {
    super.initState();
    ref.listenManual(widget.items, (_, next) {
      if (next.value case final items?) {
        _onLoaded(items);
      }
    }, fireImmediately: true);
  }

  void _onLoaded(List<ItemEntry> items) {
    final ids = [for (final item in items) item.id];
    final focus = _pendingFocus;
    _pendingFocus = null;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      _selection.retainOnly(ids);
      if (focus != null && ids.contains(focus)) {
        _selection.select(focus);
      }
    });
  }

  List<ItemEntry> _targetsFor(ItemEntry entry, List<ItemEntry> entries) {
    final selection = ref.read(selectionControllerProvider(widget.scope));
    if (selection.contains(entry.id)) {
      return entries.where((item) => selection.contains(item.id)).toList();
    }
    _selection.select(entry.id);
    return [entry];
  }

  Future<void> _showActions(List<ItemEntry> targets, Offset position) async {
    final action = await showItemActions(context, entries: targets, position: position, actions: widget.menuActions);
    if (action != null && mounted) {
      await FileActions(context).perform(action, targets);
    }
  }

  void _tap(ItemEntry entry, List<String> order, Selection selection) {
    final keyboard = HardwareKeyboard.instance;
    if (keyboard.isShiftPressed) {
      _selection.extendTo(entry.id, order);
    } else if (selection.checkboxMode || keyboard.isControlPressed || keyboard.isMetaPressed) {
      _selection.toggle(entry.id);
    } else {
      _selection.select(entry.id);
    }
  }

  ItemDragging? _dragging(List<ItemEntry> selected, Selection selection) {
    if (widget.folder == null) {
      return null;
    }
    final selectedMovable = ItemAction.move.isEnabledFor(selected);
    return ItemDragging(
      itemsFor: (entry) => switch (selection.contains(entry.id)) {
        true => selectedMovable ? selected : null,
        false => ItemAction.move.isEnabledFor([entry]) ? [entry] : null,
      },
      onStarted: (entry) {
        if (!selection.contains(entry.id)) {
          _selection.select(entry.id);
        }
      },
    );
  }

  ItemInteractions _interactions(List<ItemEntry> entries, List<ItemEntry> selected, Selection selection) {
    final order = [for (final entry in entries) entry.id];
    return ItemInteractions(
      selection: selection,
      openOnTap: WindowSizeClass.of(context) == WindowSizeClass.compact,
      onTap: (entry) => _tap(entry, order, selection),
      onOpen: (entry) => unawaited(FileActions(context).open(entry)),
      onLongPress: (entry) => _selection.startCheckboxMode(entry.id),
      onContextMenu: (entry, position) => unawaited(_showActions(_targetsFor(entry, entries), position)),
      onMore: (entry, anchor) => unawaited(_showActions(_targetsFor(entry, entries), anchorPosition(anchor))),
      keyFor: _keys.keyFor,
      decorate: (entry, child) => entry.kind == ItemKind.folder
          ? FileDropTarget(folderId: entry.id, enabled: entry.permissions.canAddChildren, child: child)
          : child,
      source: ref.read(filesServiceProvider),
      dragging: _dragging(selected, selection),
    );
  }

  Widget _header(List<ItemEntry> selected, Selection selection) {
    if (selection.isEmpty) {
      return widget.header;
    }
    final l10n = AppLocalizations.of(context);
    return SelectionToolbar(
      count: selected.length,
      onClear: _selection.clear,
      onMore: (anchor) => unawaited(_showActions(selected, anchorPosition(anchor))),
      actions: [
        for (final action in widget.toolbarActions.where((action) => action.appliesTo(selected)))
          ToolbarAction(
            icon: action.icon,
            label: action.label(l10n),
            onPressed: action.isEnabledFor(selected)
                ? () => unawaited(FileActions(context).perform(action, selected))
                : null,
          ),
      ],
    );
  }

  Widget _withDropTarget(Widget child) {
    final folder = widget.folder;
    return folder == null
        ? child
        : FileDropTarget(folderId: folder.id, enabled: folder.permissions.canAddChildren, child: child);
  }

  void Function(Offset position)? _newItemMenu() {
    final folder = widget.folder;
    return folder != null && folder.permissions.canAddChildren
        ? (position) => unawaited(showNewItemMenu(context, folderId: folder.id, position: position))
        : null;
  }

  Widget _contents(List<ItemEntry> entries, Selection selection, {required double reservedWidth}) {
    final layout = ref.watch(folderLayoutsProvider.select((layouts) => layouts.of(widget.layoutKey)));
    final scrollKey = '${widget.layoutKey}:${layout.name}';
    final selected = entries.where((entry) => selection.contains(entry.id)).toList();
    final interactions = _interactions(entries, selected, selection);
    void selectAll() => _selection.selectAll([for (final entry in entries) entry.id]);

    return _withDropTarget(
      CallbackShortcuts(
        bindings: {
          const SingleActivator(LogicalKeyboardKey.keyA, control: true): selectAll,
          const SingleActivator(LogicalKeyboardKey.keyA, meta: true): selectAll,
          const SingleActivator(LogicalKeyboardKey.escape): _selection.clear,
          const SingleActivator(LogicalKeyboardKey.delete): () {
            if (ItemAction.trash.isEnabledFor(selected)) {
              unawaited(FileActions(context).trash(selected));
            }
          },
        },
        child: Focus(
          autofocus: true,
          child: DragSelectArea(
            registry: _keys,
            onBackgroundTap: _selection.clear,
            onBackgroundSecondaryTap: _newItemMenu(),
            onSelect: (ids, {required additive}) =>
                _selection.replace(Selection(ids: additive ? {...selection.ids, ...ids} : ids)),
            child: switch ((entries.isEmpty, layout)) {
              (true, _) => widget.empty,
              (false, FileLayout.list) => FileListView(
                entries: entries,
                interactions: interactions,
                scrollKey: scrollKey,
              ),
              (false, FileLayout.grid) => FileGridView(
                reservedWidth: reservedWidth,
                entries: entries,
                interactions: interactions,
                scrollKey: scrollKey,
              ),
            },
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final itemsState = ref.watch(widget.items);
    final selection = ref.watch(selectionControllerProvider(widget.scope));
    final settings = ref.watch(fileViewSettingsProvider);
    final detailsOpen = ref.watch(detailsPanelProvider);
    final sizeClass = WindowSizeClass.of(context);
    final entries = arrangeEntries(itemsState.value ?? const [], settings);
    final selected = entries.where((entry) => selection.contains(entry.id)).toList();

    final body = switch (itemsState) {
      AsyncValue(hasValue: true) => _contents(
        entries,
        selection,
        reservedWidth: detailsOpen && sizeClass == WindowSizeClass.expanded ? ItemBrowser._panelWidth + 1 : 0,
      ),
      AsyncValue(:final error?) => widget.errorView(error),
      _ => Center(child: CircularProgressIndicator(semanticsLabel: AppLocalizations.of(context).loading)),
    };
    final page = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _header(selected, selection),
        Expanded(child: body),
      ],
    );
    if (!detailsOpen || sizeClass == WindowSizeClass.compact) {
      return page;
    }

    final panel = SizedBox(
      width: ItemBrowser._panelWidth,
      child: DetailsPanel(
        entry: selected.length == 1 ? selected.single : (selected.isEmpty ? widget.folder : null),
        selectedCount: selected.length,
        onClose: ref.read(detailsPanelProvider.notifier).hide,
      ),
    );
    if (sizeClass == WindowSizeClass.expanded) {
      return Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Expanded(child: page),
          const VerticalDivider(width: 1),
          panel,
        ],
      );
    }
    return Stack(
      children: [
        Positioned.fill(child: page),
        PositionedDirectional(
          top: 0,
          bottom: 0,
          end: 0,
          child: Material(elevation: 4, color: Theme.of(context).colorScheme.surface, child: panel),
        ),
      ],
    );
  }
}
