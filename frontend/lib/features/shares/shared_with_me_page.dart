import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/folder_header.dart';
import '../files/folder_states.dart';
import '../files/item_action.dart';
import '../files/item_browser.dart';
import 'shared_with_me.dart';

class SharedWithMePage extends ConsumerWidget {
  const SharedWithMePage({super.key});

  static const layoutKey = Routes.shared;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return ItemBrowser(
      scope: layoutKey,
      layoutKey: layoutKey,
      items: sharedWithMeProvider,
      header: ItemsHeader(title: Text(l10n.navigationSharedWithMe), layoutKey: layoutKey),
      empty: FolderMessage(
        icon: FluentIcons.people_24_regular,
        title: l10n.sharedWithMeEmptyTitle,
        message: l10n.sharedWithMeEmptyMessage,
      ),
      errorView: (error) =>
          FolderErrorView(isRoot: true, error: error, onRetry: () => ref.invalidate(sharedWithMeProvider)),
      menuActions: const [ItemAction.open, ItemAction.download],
      toolbarActions: const [ItemAction.download, ItemAction.details],
    );
  }
}
