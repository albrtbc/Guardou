import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../api/generated/models/trash_entry.dart';
import '../../app/router/routes.dart';
import '../../app/theme/app_tokens.dart';
import '../configuration/configuration_controller.dart';
import '../../core/layout/window_size_class.dart';
import '../../core/widgets/page_header.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/drag_select_area.dart';
import '../files/file_actions.dart';
import '../files/file_presentation.dart';
import '../files/files_service.dart';
import '../files/folder_states.dart';
import '../files/item_actions_menu.dart';
import '../files/item_interactions.dart';
import '../files/item_marks.dart';
import '../files/selection.dart';
import '../files/selection_controller.dart';
import '../files/selection_toolbar.dart';
import 'trash_controller.dart';

enum TrashAction {
  restore(FluentIcons.arrow_counterclockwise_24_regular),
  deleteForever(FluentIcons.delete_24_regular);

  const TrashAction(this.icon);

  final IconData icon;

  String label(AppLocalizations l10n) => switch (this) {
    restore => l10n.restore,
    deleteForever => l10n.deleteForever,
  };

  Future<void> perform(FileActions actions, List<String> ids) => switch (this) {
    restore => actions.restore(ids),
    deleteForever => actions.deleteForever(ids),
  };
}

class TrashPage extends ConsumerStatefulWidget {
  const TrashPage({super.key});

  static const selectionScope = Routes.trash;

  @override
  ConsumerState<TrashPage> createState() => _TrashPageState();
}

class _TrashPageState extends ConsumerState<TrashPage> {
  final _keys = ItemKeyRegistry();

  SelectionController get _selection => ref.read(selectionControllerProvider(TrashPage.selectionScope).notifier);

  @override
  void initState() {
    super.initState();
    ref.listenManual(trashControllerProvider, (_, next) {
      if (next.value case final entries?) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) {
            _selection.retainOnly(entries.map((entry) => entry.entry.id));
          }
        });
      }
    });
  }

  Future<void> _perform(TrashAction action, List<String> ids) => action.perform(FileActions(context), ids);

  List<String> _targetsFor(ItemEntry entry) {
    final selection = ref.read(selectionControllerProvider(TrashPage.selectionScope));
    if (selection.contains(entry.id)) {
      return selection.ids.toList();
    }
    _selection.select(entry.id);
    return [entry.id];
  }

  Future<void> _showActions(ItemEntry entry, Offset position) async {
    final ids = _targetsFor(entry);
    final l10n = AppLocalizations.of(context);
    final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;
    final action = compact
        ? await showModalBottomSheet<TrashAction>(
            context: context,
            showDragHandle: true,
            builder: (context) => SafeArea(
              child: ListView(
                shrinkWrap: true,
                children: [
                  for (final action in TrashAction.values)
                    ListTile(
                      leading: Icon(action.icon),
                      title: Text(action.label(l10n)),
                      onTap: () => Navigator.of(context).pop(action),
                    ),
                ],
              ),
            ),
          )
        : await showMenu<TrashAction>(
            context: context,
            position: menuPositionAt(context, position),
            items: [
              for (final action in TrashAction.values) PopupMenuItem(value: action, child: Text(action.label(l10n))),
            ],
          );
    if (action != null && mounted) {
      await _perform(action, ids);
    }
  }

  void _tap(ItemEntry entry, Selection selection, List<String> order) {
    final keyboard = HardwareKeyboard.instance;
    if (keyboard.isShiftPressed) {
      _selection.extendTo(entry.id, order);
    } else if (selection.checkboxMode || keyboard.isControlPressed || keyboard.isMetaPressed) {
      _selection.toggle(entry.id);
    } else {
      _selection.select(entry.id);
    }
  }

  ItemInteractions _interactions(Selection selection, List<String> order) => ItemInteractions(
    selection: selection,
    openOnTap: false,
    onTap: (entry) => _tap(entry, selection, order),
    onOpen: (entry) => _selection.select(entry.id),
    onLongPress: (entry) => _selection.startCheckboxMode(entry.id),
    onContextMenu: (entry, position) => unawaited(_showActions(entry, position)),
    onMore: (entry, anchor) => unawaited(_showActions(entry, anchorPosition(anchor))),
    keyFor: _keys.keyFor,
    decorate: (_, child) => child,
    source: ref.read(filesServiceProvider),
  );

  Widget _header(List<TrashEntry> entries, Selection selection) {
    final l10n = AppLocalizations.of(context);
    if (selection.isEmpty) {
      return PageHeader(
        child: Row(
          children: [
            Expanded(child: Text(l10n.navigationTrash)),
            TextButton.icon(
              onPressed: entries.isEmpty ? null : () => unawaited(FileActions(context).emptyTrash()),
              icon: const Icon(FluentIcons.delete_24_regular, size: 18),
              label: Text(l10n.emptyTrash),
            ),
          ],
        ),
      );
    }
    final ids = selection.ids.toList();
    return SelectionToolbar(
      count: ids.length,
      onClear: _selection.clear,
      actions: [
        for (final action in TrashAction.values)
          ToolbarAction(
            icon: action.icon,
            label: action.label(l10n),
            onPressed: () => unawaited(_perform(action, ids)),
          ),
      ],
    );
  }

  Widget _list(List<TrashEntry> entries, Selection selection) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    if (entries.isEmpty) {
      return FolderMessage(
        icon: FluentIcons.delete_24_regular,
        title: l10n.trashEmptyTitle,
        message: l10n.trashEmptyMessage,
      );
    }
    final interactions = _interactions(selection, [for (final entry in entries) entry.entry.id]);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (ref.watch(configurationControllerProvider).value?.trashRetentionDays case final days?)
          Padding(
            padding: EdgeInsets.symmetric(horizontal: spacing, vertical: spacing / 2),
            child: Text(l10n.trashRetention(days), style: Theme.of(context).textTheme.bodySmall),
          ),
        Expanded(
          child: CallbackShortcuts(
            bindings: {
              const SingleActivator(LogicalKeyboardKey.keyA, control: true): () =>
                  _selection.selectAll([for (final entry in entries) entry.entry.id]),
              const SingleActivator(LogicalKeyboardKey.escape): _selection.clear,
            },
            child: Focus(
              autofocus: true,
              child: DragSelectArea(
                registry: _keys,
                onBackgroundTap: _selection.clear,
                onSelect: (ids, {required additive}) =>
                    _selection.replace(Selection(ids: additive ? {...selection.ids, ...ids} : ids)),
                child: ListView.builder(
                  itemCount: entries.length,
                  itemBuilder: (context, index) => _TrashRow(entry: entries[index], interactions: interactions),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final trash = ref.watch(trashControllerProvider);
    final selection = ref.watch(selectionControllerProvider(TrashPage.selectionScope));
    final entries = trash.value ?? const <TrashEntry>[];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _header(entries, selection),
        Expanded(
          child: switch (trash) {
            AsyncValue(hasValue: true) => _list(entries, selection),
            AsyncValue(:final error?) => FolderErrorView(
              isRoot: true,
              error: error,
              onRetry: () => ref.invalidate(trashControllerProvider),
            ),
            _ => Center(child: CircularProgressIndicator(semanticsLabel: AppLocalizations.of(context).loading)),
          },
        ),
      ],
    );
  }
}

class _TrashRow extends StatelessWidget {
  const _TrashRow({required this.entry, required this.interactions});

  final TrashEntry entry;
  final ItemInteractions interactions;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final item = entry.entry;
    return SelectableItem(
      entry: item,
      interactions: interactions,
      child: ListTile(
        leading: ItemLeading(entry: item, interactions: interactions, size: 24),
        title: ItemName(entry: item),
        subtitle: Text(
          l10n.trashedSummary(formatDate(entry.trashed, l10n.localeName), formatDate(entry.expires, l10n.localeName)),
        ),
        trailing: ItemMoreButton(entry: item, interactions: interactions),
      ),
    );
  }
}
