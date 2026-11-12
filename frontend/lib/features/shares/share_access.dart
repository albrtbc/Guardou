import 'package:flutter/widgets.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/share_link.dart';
import '../../l10n/generated/app_localizations.dart';

enum ShareAccess {
  restricted(FluentIcons.lock_closed_24_regular),
  anyoneWithLink(FluentIcons.globe_24_regular);

  const ShareAccess(this.icon);

  final IconData icon;

  static ShareAccess of(ShareLink? link) => link == null ? restricted : anyoneWithLink;

  String label(AppLocalizations l10n) => switch (this) {
    restricted => l10n.shareAccessRestricted,
    anyoneWithLink => l10n.shareAccessAnyoneWithLink,
  };

  String description(AppLocalizations l10n) => switch (this) {
    restricted => l10n.shareAccessRestrictedDescription,
    anyoneWithLink => l10n.shareAccessAnyoneWithLinkDescription,
  };
}
