import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/layout/window_size_class.dart';
import '../../l10n/generated/app_localizations.dart';
import 'file_presentation.dart';
import 'file_view_settings.dart';
import 'item_interactions.dart';
import 'item_marks.dart';
import 'remembered_scroll.dart';
import 'sort_menu.dart';

const _rowHeight = 48.0;
const _actionsWidth = 48.0;

const _columnWidths = {
  SortField.modified: 140.0,
  SortField.created: 140.0,
  SortField.size: 100.0,
  SortField.kind: 120.0,
};

List<SortField> _columnsFor(WindowSizeClass sizeClass) => switch (sizeClass) {
  WindowSizeClass.compact => const [],
  WindowSizeClass.medium => const [SortField.modified, SortField.size],
  WindowSizeClass.expanded => const [SortField.modified, SortField.created, SortField.size, SortField.kind],
};

class FileListView extends StatelessWidget {
  const FileListView({super.key, required this.entries, required this.interactions, required this.scrollKey});

  final List<ItemEntry> entries;
  final ItemInteractions interactions;
  final String scrollKey;

  @override
  Widget build(BuildContext context) {
    final sizeClass = WindowSizeClass.of(context);

    if (sizeClass == WindowSizeClass.compact) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SortMenu(),
          Expanded(
            child: RememberedScroll(
              storageKey: scrollKey,
              builder: (context, controller) => ListView.builder(
                controller: controller,
                itemCount: entries.length,
                itemBuilder: (context, index) => _CompactRow(entry: entries[index], interactions: interactions),
              ),
            ),
          ),
        ],
      );
    }

    final columns = _columnsFor(sizeClass);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _HeaderRow(columns: columns),
        const Divider(height: 1),
        Expanded(
          child: RememberedScroll(
            storageKey: scrollKey,
            builder: (context, controller) => ListView.separated(
              controller: controller,
              itemCount: entries.length,
              separatorBuilder: (_, _) => const Divider(height: 1),
              itemBuilder: (context, index) =>
                  _WideRow(entry: entries[index], columns: columns, interactions: interactions),
            ),
          ),
        ),
      ],
    );
  }
}

class _HeaderRow extends StatelessWidget {
  const _HeaderRow({required this.columns});

  final List<SortField> columns;

  @override
  Widget build(BuildContext context) {
    final spacing = AppTokens.of(context).spacing;
    return SizedBox(
      height: _rowHeight,
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: spacing / 2),
        child: Row(
          children: [
            const Expanded(child: _HeaderCell(field: SortField.name)),
            for (final field in columns)
              SizedBox(
                width: _columnWidths[field],
                child: _HeaderCell(field: field),
              ),
            const SizedBox(width: _actionsWidth),
          ],
        ),
      ),
    );
  }
}

class _HeaderCell extends ConsumerWidget {
  const _HeaderCell({required this.field});

  final SortField field;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(fileViewSettingsProvider);
    final sorted = settings.sortField == field;

    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: TextButton(
        onPressed: () => ref.read(fileViewSettingsProvider.notifier).sortBy(field),
        style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.onSurface),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Flexible(child: Text(sortFieldLabel(field, AppLocalizations.of(context)), overflow: TextOverflow.ellipsis)),
            if (sorted) ...[const SizedBox(width: 4), Icon(sortDirectionIcon(ascending: settings.ascending), size: 16)],
          ],
        ),
      ),
    );
  }
}

class _ItemName extends StatelessWidget {
  const _ItemName({required this.entry});

  final ItemEntry entry;

  @override
  Widget build(BuildContext context) {
    final readOnly = !entry.permissions.canModify;
    return Row(
      children: [
        Flexible(child: ItemName(entry: entry)),
        if (readOnly) ...[
          const SizedBox(width: 6),
          Tooltip(
            message: AppLocalizations.of(context).readOnly,
            child: Icon(
              FluentIcons.lock_closed_24_regular,
              size: 14,
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ],
    );
  }
}

class _WideRow extends StatelessWidget {
  const _WideRow({required this.entry, required this.columns, required this.interactions});

  final ItemEntry entry;
  final List<SortField> columns;
  final ItemInteractions interactions;

  String _cell(SortField field, AppLocalizations l10n) => switch (field) {
    SortField.name => entry.name,
    SortField.modified => formatDate(entry.modified, l10n.localeName),
    SortField.created => formatDate(entry.created, l10n.localeName),
    SortField.size => formatSize(entry.size, l10n.localeName),
    SortField.kind => kindLabel(entry, l10n),
  };

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final secondary = Theme.of(
      context,
    ).textTheme.bodyMedium?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant);

    return SelectableItem(
      entry: entry,
      interactions: interactions,
      child: SizedBox(
        height: _rowHeight,
        child: Padding(
          padding: EdgeInsets.only(left: spacing),
          child: Row(
            children: [
              ItemLeading(entry: entry, interactions: interactions),
              SizedBox(width: spacing),
              Expanded(child: _ItemName(entry: entry)),
              for (final field in columns)
                SizedBox(
                  width: _columnWidths[field],
                  child: Padding(
                    padding: EdgeInsets.only(left: spacing / 2),
                    child: Text(_cell(field, l10n), style: secondary, overflow: TextOverflow.ellipsis),
                  ),
                ),
              SizedBox(
                width: _actionsWidth,
                child: ItemMoreButton(entry: entry, interactions: interactions),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CompactRow extends StatelessWidget {
  const _CompactRow({required this.entry, required this.interactions});

  final ItemEntry entry;
  final ItemInteractions interactions;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final modified = formatDate(entry.modified, l10n.localeName);

    return SelectableItem(
      entry: entry,
      interactions: interactions,
      child: ListTile(
        leading: ItemLeading(entry: entry, interactions: interactions, size: 24),
        title: _ItemName(entry: entry),
        subtitle: Text(l10n.fileSummary(modified, formatSize(entry.size, l10n.localeName))),
        trailing: ItemMoreButton(entry: entry, interactions: interactions),
      ),
    );
  }
}
