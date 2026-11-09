import 'dart:math';

import 'package:clock/clock.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/models/current_user.dart';
import '../../api/generated/models/item_entry.dart';
import '../../api/generated/models/item_kind.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/widgets/user_avatar.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/session_controller.dart';
import 'file_presentation.dart';
import 'item_interactions.dart';
import 'item_marks.dart';
import 'item_thumbnail.dart';
import 'remembered_scroll.dart';
import 'sort_menu.dart';
import 'item_type_presentation.dart';

class FileGridView extends StatelessWidget {
  const FileGridView({
    super.key,
    required this.entries,
    required this.interactions,
    required this.scrollKey,
    this.reservedWidth = 0,
  });

  static const _minimumColumns = 2;
  static const _minimumTileWidth = 250.0;
  static const _minimumScaledTileWidth = 160.0;
  static const _folderHeight = 48.0;
  static const _radius = 12.0;

  final List<ItemEntry> entries;
  final ItemInteractions interactions;
  final String scrollKey;
  final double reservedWidth;

  @override
  Widget build(BuildContext context) {
    final spacing = AppTokens.of(context).spacing;
    final folders = entries.where((entry) => entry.kind == ItemKind.folder).toList();
    final files = entries.where((entry) => entry.kind != ItemKind.folder).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SortMenu(),
        Expanded(
          child: RememberedScroll(
            storageKey: scrollKey,
            builder: (context, controller) => CustomScrollView(
              controller: controller,
              slivers: [
                SliverPadding(
                  padding: EdgeInsets.all(spacing),
                  sliver: SliverLayoutBuilder(
                    builder: (context, constraints) {
                      final width = constraints.crossAxisExtent;
                      final columns = _columnsFor(width, spacing);
                      final tileWidth = _tileWidth(width, columns, spacing);
                      return SliverGrid.builder(
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: columns,
                          mainAxisExtent: _folderHeight,
                          mainAxisSpacing: spacing,
                          crossAxisSpacing: spacing,
                        ),
                        itemCount: folders.length,
                        itemBuilder: (context, index) =>
                            _FolderTile(entry: folders[index], interactions: interactions, tileWidth: tileWidth),
                      );
                    },
                  ),
                ),
                SliverPadding(
                  padding: EdgeInsets.fromLTRB(spacing, 0, spacing, spacing),
                  sliver: SliverLayoutBuilder(
                    builder: (context, constraints) {
                      final width = constraints.crossAxisExtent;
                      final columns = _columnsFor(width, spacing);
                      final tileWidth = _tileWidth(width, columns, spacing);
                      return SliverGrid.builder(
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: columns,
                          mainAxisExtent: _FileCard.heightFor(tileWidth, spacing),
                          mainAxisSpacing: spacing,
                          crossAxisSpacing: spacing,
                        ),
                        itemCount: files.length,
                        itemBuilder: (context, index) =>
                            _FileCard(entry: files[index], interactions: interactions, tileWidth: tileWidth),
                      );
                    },
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  int _columnsFor(double width, double spacing) =>
      min(_fit(width + reservedWidth, _minimumTileWidth, spacing), _fit(width, _minimumScaledTileWidth, spacing));

  static double _tileWidth(double width, int columns, double spacing) => (width - spacing * (columns - 1)) / columns;

  static int _fit(double width, double tileWidth, double spacing) =>
      max(_minimumColumns, ((width + spacing) / (tileWidth + spacing)).floor());

  static ShapeBorder _shape() => RoundedRectangleBorder(borderRadius: BorderRadius.circular(_radius));

  static Color _cardColor(BuildContext context) => Theme.of(context).colorScheme.surfaceContainerLow;
}

class _FolderTile extends StatelessWidget {
  const _FolderTile({required this.entry, required this.interactions, required this.tileWidth});

  final ItemEntry entry;
  final ItemInteractions interactions;
  final double tileWidth;

  @override
  Widget build(BuildContext context) {
    final spacing = AppTokens.of(context).spacing;
    return SelectableItem(
      entry: entry,
      interactions: interactions,
      shape: FileGridView._shape(),
      color: FileGridView._cardColor(context),
      dragFeedbackWidth: tileWidth,
      child: Padding(
        padding: EdgeInsetsDirectional.only(start: spacing),
        child: Row(
          children: [
            ItemLeading(entry: entry, interactions: interactions),
            SizedBox(width: spacing),
            Expanded(child: Text(entry.name, maxLines: 1, overflow: TextOverflow.ellipsis)),
            ItemIndicators(entry: entry),
            ItemMoreButton(entry: entry, interactions: interactions),
          ],
        ),
      ),
    );
  }
}

class _FileCard extends ConsumerWidget {
  const _FileCard({required this.entry, required this.interactions, required this.tileWidth});

  static const _headerHeight = 52.0;
  static const _footerHeight = 28.0;
  static const _previewAspectRatio = 4 / 3;
  static const _iconSize = 20.0;
  static const _placeholderIconSize = 56.0;
  static const _previewRadius = 8.0;
  static const _avatarRadius = 12.0;

  final ItemEntry entry;
  final ItemInteractions interactions;
  final double tileWidth;

  static double heightFor(double width, double spacing) {
    final insets = _insets(spacing);
    final previewHeight = (width - insets.horizontal) / _previewAspectRatio;
    return _headerHeight + previewHeight + _gap(spacing) + _footerHeight + insets.bottom;
  }

  static EdgeInsets _insets(double spacing) => EdgeInsets.fromLTRB(spacing * 0.75, 0, spacing * 0.75, spacing * 0.75);

  static double _gap(double spacing) => spacing / 2;

  String _cardDate(AppLocalizations l10n, DateTime date) => formatCardDate(date, clock.now(), l10n.localeName);

  Widget _lastEventAvatar(CurrentUser? user) {
    if (entry.opened != null && user != null) {
      return UserAvatar(userId: user.id, name: user.displayName, hasPicture: user.hasPicture, radius: _avatarRadius);
    }
    return UserAvatar(
      userId: entry.creator.id,
      name: entry.creator.displayName,
      hasPicture: entry.creator.hasPicture,
      pictureUri: interactions.source.creatorPictureUri(entry.id),
      radius: _avatarRadius,
    );
  }

  String _lastEventLabel(AppLocalizations l10n, CurrentUser? user) {
    final opened = entry.opened;
    if (opened != null && user != null) {
      return l10n.fileOpenedByYou(_cardDate(l10n, opened));
    }
    final date = _cardDate(l10n, entry.created);
    final creator = entry.creator;
    return creator.id != null && creator.id == user?.id
        ? l10n.fileCreatedByYou(date)
        : l10n.fileCreatedBy(creator.displayName, date);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final spacing = AppTokens.of(context).spacing;
    final user = ref.watch(sessionControllerProvider).value;
    final placeholder = Center(child: ItemTypeIcon(entry.type, size: _placeholderIconSize));

    return SelectableItem(
      entry: entry,
      interactions: interactions,
      shape: FileGridView._shape(),
      color: FileGridView._cardColor(context),
      dragFeedbackWidth: tileWidth,
      child: Padding(
        padding: _insets(spacing),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              height: _headerHeight,
              child: Row(
                children: [
                  ItemLeading(entry: entry, interactions: interactions, size: _iconSize, showPreview: false),
                  SizedBox(width: spacing * 0.75),
                  Expanded(
                    child: Text(
                      entry.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleSmall,
                    ),
                  ),
                  ItemIndicators(entry: entry),
                  ItemMoreButton(entry: entry, interactions: interactions),
                ],
              ),
            ),
            AspectRatio(
              aspectRatio: _previewAspectRatio,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(_previewRadius),
                child: ColoredBox(
                  color: theme.colorScheme.surface,
                  child: entry.hasPreview
                      ? ItemThumbnail(
                          key: ValueKey(entry.id),
                          entry: entry,
                          source: interactions.source,
                          placeholder: placeholder,
                        )
                      : placeholder,
                ),
              ),
            ),
            SizedBox(height: _gap(spacing)),
            SizedBox(
              height: _footerHeight,
              child: Row(
                children: [
                  _lastEventAvatar(user),
                  SizedBox(width: spacing / 2),
                  Expanded(
                    child: Text(
                      _lastEventLabel(l10n, user),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
