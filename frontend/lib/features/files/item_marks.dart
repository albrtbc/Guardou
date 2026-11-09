import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../l10n/generated/app_localizations.dart';

class ItemName extends StatelessWidget {
  const ItemName({super.key, required this.entry, this.style});

  final ItemEntry entry;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Flexible(
          child: Text(entry.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: style),
        ),
        ItemIndicators(entry: entry),
      ],
    );
  }
}

class ItemIndicators extends StatelessWidget {
  const ItemIndicators({super.key, required this.entry});

  final ItemEntry entry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (entry.sharedByMe) _Indicator(icon: FluentIcons.people_24_filled, label: l10n.itemSharedByMe),
        if (entry.starred) _Indicator(icon: FluentIcons.star_24_filled, label: l10n.navigationStarred),
      ],
    );
  }
}

class _Indicator extends StatelessWidget {
  const _Indicator({required this.icon, required this.label});

  static const _size = 16.0;
  static const _gap = 4.0;

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsetsDirectional.only(start: _gap),
      child: Icon(icon, size: _size, color: Theme.of(context).colorScheme.onSurfaceVariant, semanticLabel: label),
    );
  }
}
