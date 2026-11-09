import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import 'item_action.dart';
import 'item_type_presentation.dart';

@immutable
class DraggedItems {
  const DraggedItems(this.entries);

  final List<ItemEntry> entries;

  bool canMoveTo(String folderId) =>
      ItemAction.move.isEnabledFor(entries) &&
      entries.every((entry) => entry.id != folderId && entry.parentId != folderId);
}

@immutable
class ItemDragging {
  const ItemDragging({required this.itemsFor, required this.onStarted});

  final List<ItemEntry>? Function(ItemEntry entry) itemsFor;
  final ValueChanged<ItemEntry> onStarted;
}

class ItemDraggable extends Draggable<DraggedItems> {
  ItemDraggable({
    super.key,
    required DraggedItems items,
    required double feedbackWidth,
    super.onDragStarted,
    required super.child,
  }) : super(
         data: items,
         maxSimultaneousDrags: 1,
         dragAnchorStrategy: _anchor,
         feedback: _ItemDragFeedback(entries: items.entries, width: feedbackWidth),
       );

  static const defaultFeedbackWidth = 250.0;

  static Offset _anchor(Draggable<Object> draggable, BuildContext context, Offset position) =>
      const Offset(_ItemDragFeedback.height / 2, _ItemDragFeedback.height / 2);

  @override
  MultiDragGestureRecognizer createRecognizer(GestureMultiDragStartCallback onStart) =>
      ImmediateMultiDragGestureRecognizer(
        supportedDevices: const {PointerDeviceKind.mouse},
        allowedButtonsFilter: allowedButtonsFilter,
      )..onStart = onStart;
}

class _ItemDragFeedback extends StatelessWidget {
  const _ItemDragFeedback({required this.entries, required this.width});

  static const height = 48.0;
  static const _radius = 12.0;
  static const _iconSize = 20.0;
  static const _elevation = 6.0;

  final List<ItemEntry> entries;
  final double width;

  @override
  Widget build(BuildContext context) {
    final spacing = AppTokens.of(context).spacing;
    final colorScheme = Theme.of(context).colorScheme;
    final single = entries.length == 1 ? entries.single : null;
    return SizedBox(
      width: width,
      height: height,
      child: Material(
        elevation: _elevation,
        color: colorScheme.secondaryContainer,
        textStyle: Theme.of(context).textTheme.bodyMedium?.copyWith(color: colorScheme.onSecondaryContainer),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(_radius)),
        child: Padding(
          padding: EdgeInsetsDirectional.symmetric(horizontal: spacing),
          child: Row(
            children: [
              if (single != null)
                ItemTypeIcon(single.type, size: _iconSize)
              else
                const Icon(FluentIcons.document_copy_24_regular, size: _iconSize),
              SizedBox(width: spacing),
              Expanded(
                child: Text(
                  single?.name ?? AppLocalizations.of(context).selectedCount(entries.length),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
