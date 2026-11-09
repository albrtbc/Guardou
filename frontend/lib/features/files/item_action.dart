import 'package:flutter/widgets.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../l10n/generated/app_localizations.dart';

enum ItemAction {
  open(FluentIcons.open_24_regular),
  download(FluentIcons.arrow_download_24_regular),
  share(FluentIcons.person_add_24_regular),
  star(FluentIcons.star_24_regular),
  unstar(FluentIcons.star_off_24_regular),
  rename(FluentIcons.edit_24_regular),
  move(FluentIcons.folder_arrow_right_24_regular),
  copy(FluentIcons.copy_24_regular),
  details(FluentIcons.info_24_regular),
  trash(FluentIcons.delete_24_regular);

  const ItemAction(this.icon);

  final IconData icon;

  String label(AppLocalizations l10n) => switch (this) {
    open => l10n.actionOpen,
    download => l10n.actionDownload,
    share => l10n.actionShare,
    star => l10n.actionStar,
    unstar => l10n.actionUnstar,
    rename => l10n.actionRename,
    move => l10n.actionMove,
    copy => l10n.actionCopy,
    details => l10n.actionDetails,
    trash => l10n.actionTrash,
  };

  bool appliesTo(List<ItemEntry> entries) => switch (this) {
    star => !_allStarred(entries),
    unstar => _allStarred(entries),
    _ => true,
  };

  bool isEnabledFor(List<ItemEntry> entries) => switch (this) {
    _ when entries.isEmpty => false,
    open || details => entries.length == 1,
    download => true,
    rename => entries.length == 1 && entries.single.permissions.canModify,
    share => entries.length == 1 && entries.single.permissions.canModify,
    move || copy || star || unstar || trash => entries.every((entry) => entry.permissions.canModify),
  };

  static bool _allStarred(List<ItemEntry> entries) => entries.isNotEmpty && entries.every((entry) => entry.starred);
}
