import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import 'file_presentation.dart';
import 'item_type_presentation.dart';

class DetailsPanel extends StatelessWidget {
  const DetailsPanel({super.key, required this.entry, required this.selectedCount, this.onClose});

  final ItemEntry? entry;
  final int selectedCount;
  final VoidCallback? onClose;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final item = entry;

    return ListView(
      padding: EdgeInsets.all(spacing),
      children: [
        Row(
          children: [
            item == null ? const Icon(FluentIcons.document_copy_24_regular) : ItemTypeIcon(item.type),
            SizedBox(width: spacing / 2),
            Expanded(
              child: Text(
                item == null ? l10n.selectedCount(selectedCount) : folderLabel(item, l10n),
                style: Theme.of(context).textTheme.titleMedium,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            if (onClose case final close?)
              IconButton(tooltip: l10n.close, onPressed: close, icon: const Icon(FluentIcons.dismiss_24_regular)),
          ],
        ),
        if (item != null) ...[
          SizedBox(height: spacing),
          _Property(label: l10n.columnKind, value: kindLabel(item, l10n)),
          _Property(label: l10n.columnSize, value: formatSize(item.size, l10n.localeName)),
          _Property(label: l10n.columnModified, value: _formatDateTime(item.modified, l10n.localeName)),
          _Property(label: l10n.columnCreated, value: _formatDateTime(item.created, l10n.localeName)),
          if (!item.permissions.canModify) _Property(label: l10n.access, value: l10n.readOnly),
        ],
      ],
    );
  }

  static String _formatDateTime(DateTime date, String locale) =>
      DateFormat.yMMMd(locale).add_jm().format(date.toLocal());
}

class DetailsPage extends StatelessWidget {
  const DetailsPage({super.key, required this.entry});

  final ItemEntry entry;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(AppLocalizations.of(context).actionDetails)),
      body: DetailsPanel(entry: entry, selectedCount: 1),
    );
  }
}

class _Property extends StatelessWidget {
  const _Property({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: theme.textTheme.labelMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
          Text(value),
        ],
      ),
    );
  }
}
