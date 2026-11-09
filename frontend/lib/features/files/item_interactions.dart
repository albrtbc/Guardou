import 'package:clock/clock.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../l10n/generated/app_localizations.dart';
import 'file_source.dart';
import 'item_drag.dart';
import 'item_thumbnail.dart';
import 'selection.dart';
import 'item_type_presentation.dart';

@immutable
class ItemInteractions {
  const ItemInteractions({
    required this.selection,
    required this.openOnTap,
    required this.onTap,
    required this.onOpen,
    required this.onLongPress,
    required this.onContextMenu,
    required this.onMore,
    required this.keyFor,
    required this.decorate,
    required this.source,
    this.dragging,
  });

  final Selection selection;
  final bool openOnTap;
  final ValueChanged<ItemEntry> onTap;
  final ValueChanged<ItemEntry> onOpen;
  final ValueChanged<ItemEntry> onLongPress;
  final void Function(ItemEntry entry, Offset position) onContextMenu;
  final void Function(ItemEntry entry, BuildContext anchor) onMore;
  final GlobalKey Function(String id) keyFor;
  final Widget Function(ItemEntry entry, Widget child) decorate;
  final FileSource source;
  final ItemDragging? dragging;

  bool isSelected(ItemEntry entry) => selection.contains(entry.id);
}

class SelectableItem extends StatefulWidget {
  const SelectableItem({
    super.key,
    required this.entry,
    required this.interactions,
    required this.child,
    this.shape,
    this.color = Colors.transparent,
    this.dragFeedbackWidth = ItemDraggable.defaultFeedbackWidth,
  });

  final ItemEntry entry;
  final ItemInteractions interactions;
  final Widget child;
  final ShapeBorder? shape;
  final Color color;
  final double dragFeedbackWidth;

  @override
  State<SelectableItem> createState() => _SelectableItemState();
}

class _SelectableItemState extends State<SelectableItem> {
  DateTime? _lastTap;

  ItemInteractions get _interactions => widget.interactions;

  bool get _opensOnTap => _interactions.openOnTap && !_interactions.selection.checkboxMode;

  void _tap() {
    if (_opensOnTap) {
      _interactions.onOpen(widget.entry);
      return;
    }
    final now = clock.now();
    final last = _lastTap;
    if (last != null && now.difference(last) <= kDoubleTapTimeout) {
      _lastTap = null;
      _interactions.onOpen(widget.entry);
    } else {
      _lastTap = now;
      _interactions.onTap(widget.entry);
    }
  }

  Widget _draggable(Widget child) {
    final dragging = _interactions.dragging;
    final items = dragging?.itemsFor(widget.entry);
    if (dragging == null || items == null) {
      return child;
    }
    return ItemDraggable(
      items: DraggedItems(items),
      feedbackWidth: widget.dragFeedbackWidth,
      onDragStarted: () => dragging.onStarted(widget.entry),
      child: child,
    );
  }

  @override
  Widget build(BuildContext context) {
    final selected = _interactions.isSelected(widget.entry);
    final colorScheme = Theme.of(context).colorScheme;

    return KeyedSubtree(
      key: _interactions.keyFor(widget.entry.id),
      child: _interactions.decorate(
        widget.entry,
        _draggable(
          GestureDetector(
            onSecondaryTapUp: (details) => _interactions.onContextMenu(widget.entry, details.globalPosition),
            child: Material(
              color: selected ? colorScheme.secondaryContainer : widget.color,
              shape: widget.shape,
              clipBehavior: widget.shape == null ? Clip.none : Clip.antiAlias,
              child: InkWell(
                onTap: _tap,
                onLongPress: () => _interactions.onLongPress(widget.entry),
                child: widget.child,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class ItemLeading extends StatelessWidget {
  const ItemLeading({
    super.key,
    required this.entry,
    required this.interactions,
    this.size = 20,
    this.showPreview = true,
  });

  static const _thumbnailScale = 1.4;
  static const _radius = 4.0;

  final ItemEntry entry;
  final ItemInteractions interactions;
  final double size;
  final bool showPreview;

  @override
  Widget build(BuildContext context) {
    if (interactions.selection.checkboxMode) {
      return IgnorePointer(
        child: Checkbox(value: interactions.isSelected(entry), onChanged: (_) {}),
      );
    }
    return _icon();
  }

  Widget _icon() {
    final placeholder = ItemTypeIcon(entry.type, size: size);
    if (!showPreview || !entry.hasPreview) {
      return placeholder;
    }
    final extent = size * _thumbnailScale;
    return SizedBox.square(
      dimension: extent,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(_radius),
        child: ItemThumbnail(
          key: ValueKey(entry.id),
          entry: entry,
          source: interactions.source,
          placeholder: Center(child: placeholder),
        ),
      ),
    );
  }
}

class ItemMoreButton extends StatelessWidget {
  const ItemMoreButton({super.key, required this.entry, required this.interactions});

  final ItemEntry entry;
  final ItemInteractions interactions;

  @override
  Widget build(BuildContext context) {
    return Builder(
      builder: (anchor) => IconButton(
        tooltip: AppLocalizations.of(context).moreActions,
        visualDensity: VisualDensity.compact,
        onPressed: () => interactions.onMore(entry, anchor),
        icon: const Icon(FluentIcons.more_vertical_24_regular, size: 18),
      ),
    );
  }
}
