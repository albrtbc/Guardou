import 'package:flutter/widgets.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';

enum AdminSection {
  users(Routes.adminUsers, FluentIcons.people_24_regular, FluentIcons.people_24_filled),
  tasks(Routes.adminTasks, FluentIcons.task_list_ltr_24_regular, FluentIcons.task_list_ltr_24_filled),
  settings(Routes.adminSettings, FluentIcons.settings_24_regular, FluentIcons.settings_24_filled);

  const AdminSection(this.path, this.icon, this.selectedIcon);

  final String path;
  final IconData icon;
  final IconData selectedIcon;

  String label(AppLocalizations l10n) => switch (this) {
    users => l10n.users,
    tasks => l10n.tasks,
    settings => l10n.settings,
  };

  static AdminSection? fromPath(String path) {
    for (final section in values) {
      if (path == section.path || path.startsWith('${section.path}/')) {
        return section;
      }
    }
    return null;
  }
}
