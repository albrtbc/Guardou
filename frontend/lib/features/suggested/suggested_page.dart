import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/folder_header.dart';
import '../files/folder_states.dart';
import '../files/item_browser.dart';
import '../files/recent_items.dart';

class SuggestedPage extends ConsumerWidget {
  const SuggestedPage({super.key});

  static const layoutKey = Routes.suggested;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return ItemBrowser(
      scope: layoutKey,
      layoutKey: layoutKey,
      items: recentItemsProvider,
      header: ItemsHeader(title: Text(l10n.navigationSuggested), layoutKey: layoutKey),
      empty: FolderMessage(
        icon: FluentIcons.clock_24_regular,
        title: l10n.recentEmptyTitle,
        message: l10n.recentEmptyMessage,
      ),
      errorView: (error) =>
          FolderErrorView(isRoot: true, error: error, onRetry: () => ref.invalidate(recentItemsProvider)),
    );
  }
}
