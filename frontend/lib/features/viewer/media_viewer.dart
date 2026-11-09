import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/file_source.dart';
import 'viewer_frame.dart';

class MediaViewer extends StatelessWidget {
  const MediaViewer({
    super.key,
    required this.entry,
    required this.source,
    required this.siblings,
    required this.viewerRoute,
    required this.leading,
    this.closeLocation,
    this.actions = const [],
    this.footer,
  });

  final ItemEntry entry;
  final FileSource source;
  final List<ItemEntry> siblings;
  final String Function(String id) viewerRoute;
  final Widget leading;
  final String? closeLocation;
  final List<Widget> actions;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final index = siblings.indexWhere((sibling) => sibling.id == entry.id);
    final previous = index > 0 ? siblings[index - 1] : null;
    final next = index >= 0 && index < siblings.length - 1 ? siblings[index + 1] : null;
    final close = closeLocation;

    void show(ItemEntry? target) {
      if (target != null) {
        context.go(viewerRoute(target.id));
      }
    }

    return CallbackShortcuts(
      bindings: {
        const SingleActivator(LogicalKeyboardKey.arrowLeft): () => show(previous),
        const SingleActivator(LogicalKeyboardKey.arrowRight): () => show(next),
        if (close != null) const SingleActivator(LogicalKeyboardKey.escape): () => context.go(close),
      },
      child: Focus(
        autofocus: true,
        child: ViewerFrame(
          entry: entry,
          source: source,
          leading: leading,
          actions: actions,
          footer: footer,
          overlays: [
            if (previous != null)
              Align(
                alignment: Alignment.centerLeft,
                child: _StepButton(
                  icon: FluentIcons.chevron_left_24_regular,
                  tooltip: l10n.viewerPrevious,
                  onPressed: () => show(previous),
                ),
              ),
            if (next != null)
              Align(
                alignment: Alignment.centerRight,
                child: _StepButton(
                  icon: FluentIcons.chevron_right_24_regular,
                  tooltip: l10n.viewerNext,
                  onPressed: () => show(next),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class ViewerCloseButton extends StatelessWidget {
  const ViewerCloseButton({super.key, required this.location});

  final String location;

  @override
  Widget build(BuildContext context) => IconButton(
    tooltip: AppLocalizations.of(context).close,
    onPressed: () => context.go(location),
    icon: const Icon(FluentIcons.dismiss_24_regular),
  );
}

class _StepButton extends StatelessWidget {
  const _StepButton({required this.icon, required this.tooltip, required this.onPressed});

  static const _margin = 8.0;

  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.all(_margin),
    child: IconButton.filledTonal(tooltip: tooltip, onPressed: onPressed, icon: Icon(icon)),
  );
}
