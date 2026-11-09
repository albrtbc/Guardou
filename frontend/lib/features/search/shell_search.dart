import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../api/generated/models/item_kind.dart';
import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';
import 'search_controller.dart';
import 'search_panel.dart';

class ShellSearch extends ConsumerStatefulWidget {
  const ShellSearch({super.key, required this.fullScreen, this.folderId});

  static const _viewMaxHeight = 560.0;

  final bool fullScreen;
  final String? folderId;

  @override
  ConsumerState<ShellSearch> createState() => _ShellSearchState();
}

class _ShellSearchState extends ConsumerState<ShellSearch> {
  final _controller = SearchController();

  ItemSearchController get _search => ref.read(itemSearchControllerProvider.notifier);

  @override
  void initState() {
    super.initState();
    _controller.text = ref.read(itemSearchControllerProvider).text;
    _controller.addListener(() => _search.setText(_controller.text));
    WidgetsBinding.instance.addPostFrameCallback((_) => _syncFolder());
  }

  @override
  void didUpdateWidget(ShellSearch oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.folderId != widget.folderId) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _syncFolder());
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _syncFolder() {
    if (mounted) {
      _search.setFolder(widget.folderId);
    }
  }

  void _open(ItemEntry entry) {
    _controller.closeView(_controller.text);
    context.go(entry.kind == ItemKind.folder ? Routes.folder(entry.id) : Routes.viewer(entry.id));
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final color = Theme.of(context).colorScheme.onPrimaryContainer;

    return SearchAnchor(
      searchController: _controller,
      isFullScreen: widget.fullScreen,
      viewHintText: l10n.searchHint,
      viewConstraints: const BoxConstraints(maxHeight: ShellSearch._viewMaxHeight),
      viewBuilder: (_) => SearchPanel(onOpen: _open),
      suggestionsBuilder: (_, _) => const [],
      builder: (context, controller) => widget.fullScreen
          ? IconButton(
              tooltip: l10n.search,
              onPressed: controller.openView,
              icon: const Icon(FluentIcons.search_24_regular),
            )
          : SearchBar(
              controller: controller,
              elevation: const WidgetStatePropertyAll(0),
              leading: Icon(FluentIcons.search_24_regular, color: color),
              hintText: l10n.searchHint,
              hintStyle: WidgetStatePropertyAll(TextStyle(color: color)),
              onTap: controller.openView,
              onChanged: (_) => controller.openView(),
            ),
    );
  }
}
