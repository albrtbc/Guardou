import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/folder_header.dart';
import '../files/folder_states.dart';
import '../files/item_browser.dart';
import '../files/starred_items.dart';

class StarredPage extends ConsumerWidget {
  const StarredPage({super.key});

  static const layoutKey = Routes.starred;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return ItemBrowser(
      scope: layoutKey,
      layoutKey: layoutKey,
      items: starredItemsProvider,
      header: ItemsHeader(title: Text(l10n.navigationStarred), layoutKey: layoutKey),
      empty: FolderMessage(
        icon: FluentIcons.star_24_regular,
        title: l10n.starredEmptyTitle,
        message: l10n.starredEmptyMessage,
      ),
      errorView: (error) =>
          FolderErrorView(isRoot: true, error: error, onRetry: () => ref.invalidate(starredItemsProvider)),
    );
  }
}
