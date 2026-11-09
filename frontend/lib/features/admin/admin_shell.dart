import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/layout/window_size_class.dart';
import '../../l10n/generated/app_localizations.dart';
import '../shell/adaptive_shell.dart';
import '../shell/shell_top_bar.dart';
import 'admin_section.dart';

class AdminShell extends StatelessWidget {
  const AdminShell({super.key, required this.location, required this.child});

  final String location;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final selected = AdminSection.fromPath(location);
    void select(AdminSection section) => context.go(section.path);
    final l10n = AppLocalizations.of(context);

    return switch (WindowSizeClass.of(context)) {
      WindowSizeClass.compact => Scaffold(
        appBar: const ShellTopBar(showMenuButton: true),
        drawer: AdminNavigation(
          selected: selected,
          onSelected: (section) {
            Navigator.of(context).pop();
            select(section);
          },
        ),
        body: child,
      ),
      WindowSizeClass.medium => Scaffold(
        appBar: const ShellTopBar(),
        body: Row(
          children: [
            NavigationRail(
              selectedIndex: selected?.index,
              onDestinationSelected: (index) => select(AdminSection.values[index]),
              labelType: NavigationRailLabelType.all,
              backgroundColor: Colors.transparent,
              leading: IconButton(
                tooltip: l10n.backToFiles,
                icon: const Icon(FluentIcons.arrow_left_24_regular),
                onPressed: () => context.go(Routes.files),
              ),
              destinations: [
                for (final section in AdminSection.values)
                  NavigationRailDestination(
                    icon: Icon(section.icon),
                    selectedIcon: Icon(section.selectedIcon),
                    label: Text(section.label(l10n)),
                  ),
              ],
            ),
            Expanded(child: ContentPanel(child: child)),
          ],
        ),
      ),
      WindowSizeClass.expanded => Scaffold(
        appBar: const ShellTopBar(),
        body: Row(
          children: [
            SizedBox(
              width: AppTokens.of(context).navigationWidth,
              child: AdminNavigation(selected: selected, onSelected: select, inline: true),
            ),
            Expanded(child: ContentPanel(child: child)),
          ],
        ),
      ),
    };
  }
}

class AdminNavigation extends StatelessWidget {
  const AdminNavigation({super.key, required this.selected, required this.onSelected, this.inline = false});

  final AdminSection? selected;
  final ValueChanged<AdminSection> onSelected;
  final bool inline;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;

    return NavigationDrawer(
      selectedIndex: selected?.index,
      onDestinationSelected: (index) => onSelected(AdminSection.values[index]),
      backgroundColor: inline ? Colors.transparent : null,
      elevation: inline ? 0 : null,
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(spacing * 0.75, spacing / 2, spacing * 0.75, spacing / 2),
          child: Align(
            alignment: AlignmentDirectional.centerStart,
            child: TextButton.icon(
              onPressed: () => context.go(Routes.files),
              icon: const Icon(FluentIcons.arrow_left_24_regular, size: 18),
              label: Text(l10n.backToFiles),
            ),
          ),
        ),
        for (final section in AdminSection.values)
          NavigationDrawerDestination(
            icon: Icon(section.icon),
            selectedIcon: Icon(section.selectedIcon),
            label: Text(section.label(l10n)),
          ),
      ],
    );
  }
}
