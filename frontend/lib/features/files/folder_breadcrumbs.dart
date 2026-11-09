import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/breadcrumb.dart';
import '../../api/generated/models/item_details.dart';
import '../../app/router/routes.dart';
import '../../core/layout/window_size_class.dart';
import '../../l10n/generated/app_localizations.dart';
import 'file_drop_target.dart';
import 'file_presentation.dart';

String crumbRoute(Breadcrumb crumb) => crumb.name.isEmpty ? Routes.files : Routes.folder(crumb.id);

class FolderBreadcrumbs extends StatelessWidget {
  const FolderBreadcrumbs({
    super.key,
    required this.folder,
    this.routeOf = crumbRoute,
    this.acceptsDrops = true,
    this.inSharedWithMe = false,
  });

  static const _compactVisibleAncestors = 1;

  final ItemDetails folder;
  final String Function(Breadcrumb crumb) routeOf;
  final bool acceptsDrops;
  final bool inSharedWithMe;

  @override
  Widget build(BuildContext context) {
    final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;
    final ancestors = folder.ancestors;
    final collapsed = compact && ancestors.length > _compactVisibleAncestors + 1
        ? ancestors.sublist(_compactVisibleAncestors)
        : const <Breadcrumb>[];
    final visible = ancestors.take(ancestors.length - collapsed.length);

    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        reverse: true,
        child: Row(
          children: [
            if (inSharedWithMe) ...[const _SharedWithMeCrumb(), const _Separator()],
            for (final ancestor in visible) ...[
              _Crumb(crumb: ancestor, route: routeOf(ancestor), acceptsDrops: acceptsDrops),
              const _Separator(),
            ],
            if (collapsed.isNotEmpty) ...[_CollapsedCrumbs(crumbs: collapsed, routeOf: routeOf), const _Separator()],
            Text(folderLabel(folder.entry, AppLocalizations.of(context))),
          ],
        ),
      ),
    );
  }
}

class _Crumb extends StatelessWidget {
  const _Crumb({required this.crumb, required this.route, required this.acceptsDrops});

  final Breadcrumb crumb;
  final String route;
  final bool acceptsDrops;

  @override
  Widget build(BuildContext context) {
    final link = _CrumbLink(label: breadcrumbLabel(crumb, AppLocalizations.of(context)), route: route);
    return acceptsDrops ? FileDropTarget(folderId: crumb.id, enabled: true, child: link) : link;
  }
}

class _SharedWithMeCrumb extends StatelessWidget {
  const _SharedWithMeCrumb();

  @override
  Widget build(BuildContext context) =>
      _CrumbLink(label: AppLocalizations.of(context).navigationSharedWithMe, route: Routes.shared);
}

class _CrumbLink extends StatelessWidget {
  const _CrumbLink({required this.label, required this.route});

  final String label;
  final String route;

  @override
  Widget build(BuildContext context) => InkWell(
    onTap: () => context.go(route),
    borderRadius: BorderRadius.circular(8),
    child: Text(label, style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant)),
  );
}

class _CollapsedCrumbs extends StatelessWidget {
  const _CollapsedCrumbs({required this.crumbs, required this.routeOf});

  final List<Breadcrumb> crumbs;
  final String Function(Breadcrumb crumb) routeOf;

  @override
  Widget build(BuildContext context) {
    return MenuAnchor(
      builder: (context, menu, _) => IconButton(
        tooltip: AppLocalizations.of(context).moreFolders,
        onPressed: () => menu.isOpen ? menu.close() : menu.open(),
        icon: const Icon(FluentIcons.more_horizontal_24_regular),
      ),
      menuChildren: [
        for (final crumb in crumbs)
          MenuItemButton(
            leadingIcon: const Icon(FluentIcons.folder_24_regular),
            onPressed: () => context.go(routeOf(crumb)),
            child: Text(breadcrumbLabel(crumb, AppLocalizations.of(context))),
          ),
      ],
    );
  }
}

class _Separator extends StatelessWidget {
  const _Separator();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8),
      child: Icon(
        FluentIcons.chevron_right_24_regular,
        size: 18,
        color: Theme.of(context).colorScheme.onSurfaceVariant,
      ),
    );
  }
}
