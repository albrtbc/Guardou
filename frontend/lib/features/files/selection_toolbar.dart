import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../core/layout/window_size_class.dart';
import '../../core/widgets/page_header.dart';
import '../../l10n/generated/app_localizations.dart';

@immutable
class ToolbarAction {
  const ToolbarAction({required this.icon, required this.label, required this.onPressed});

  final IconData icon;
  final String label;
  final VoidCallback? onPressed;
}

class SelectionToolbar extends StatelessWidget {
  const SelectionToolbar({super.key, required this.count, required this.onClear, required this.actions, this.onMore});

  final int count;
  final VoidCallback onClear;
  final List<ToolbarAction> actions;
  final ValueChanged<BuildContext>? onMore;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;
    final visible = compact && onMore != null ? const <ToolbarAction>[] : actions;

    return PageHeader(
      child: Row(
        children: [
          IconButton(
            tooltip: l10n.clearSelection,
            onPressed: onClear,
            icon: const Icon(FluentIcons.dismiss_24_regular),
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(l10n.selectedCount(count), overflow: TextOverflow.ellipsis)),
          for (final action in visible)
            IconButton(tooltip: action.label, onPressed: action.onPressed, icon: Icon(action.icon)),
          if (onMore case final more? when compact)
            Builder(
              builder: (anchor) => IconButton(
                tooltip: l10n.moreActions,
                onPressed: () => more(anchor),
                icon: const Icon(FluentIcons.more_vertical_24_regular),
              ),
            ),
        ],
      ),
    );
  }
}
