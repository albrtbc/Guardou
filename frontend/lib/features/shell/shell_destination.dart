import 'package:flutter/widgets.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';

enum ShellDestination {
  suggested(Routes.suggested, FluentIcons.sparkle_24_regular, FluentIcons.sparkle_24_filled),
  files(Routes.files, FluentIcons.folder_24_regular, FluentIcons.folder_24_filled),
  starred(Routes.starred, FluentIcons.star_24_regular, FluentIcons.star_24_filled),
  shared(Routes.shared, FluentIcons.people_24_regular, FluentIcons.people_24_filled),
  trash(Routes.trash, FluentIcons.delete_24_regular, FluentIcons.delete_24_filled);

  const ShellDestination(this.path, this.icon, this.selectedIcon);

  static const compactBar = [suggested, starred, shared, files];
  static const navigation = [suggested, files, starred, shared, trash];

  final String path;
  final IconData icon;
  final IconData selectedIcon;

  String label(AppLocalizations l10n) => switch (this) {
    suggested => l10n.navigationSuggested,
    files => l10n.navigationFiles,
    starred => l10n.navigationStarred,
    shared => l10n.navigationSharedWithMe,
    trash => l10n.navigationTrash,
  };

  String shortLabel(AppLocalizations l10n) => this == shared ? l10n.navigationShared : label(l10n);

  static int indexIn(List<ShellDestination> destinations, ShellDestination? destination) =>
      destination == null ? -1 : destinations.indexOf(destination);

  List<String> get _paths => this == files ? const [Routes.files, Routes.folders, Routes.file] : [path];

  static ShellDestination? fromPath(String path) {
    for (final destination in values) {
      if (destination._paths.any((prefix) => path == prefix || path.startsWith('$prefix/'))) {
        return destination;
      }
    }
    return null;
  }
}
