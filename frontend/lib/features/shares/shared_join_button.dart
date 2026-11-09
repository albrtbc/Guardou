import 'package:fluentui_system_icons/fluentui_system_icons.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/layout/window_size_class.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/session_controller.dart';
import 'shared_items.dart';
import 'shares_service.dart';

class SharedJoinButton extends ConsumerStatefulWidget {
  const SharedJoinButton({super.key, required this.token, required this.itemId, required this.location});

  final String token;
  final String itemId;
  final String location;

  @override
  ConsumerState<SharedJoinButton> createState() => _SharedJoinButtonState();
}

class _SharedJoinButtonState extends ConsumerState<SharedJoinButton> {
  bool _joining = false;

  Future<void> _join() async {
    final router = GoRouter.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final error = AppLocalizations.of(context).genericError;
    setState(() => _joining = true);
    try {
      await ref.read(sharesServiceProvider).join(widget.token);
      router.go(widget.location);
    } on Object {
      messenger.showSnackBar(SnackBar(content: Text(error)));
      if (mounted) {
        setState(() => _joining = false);
      }
    }
  }

  void _open() => context.go(widget.location);

  @override
  Widget build(BuildContext context) {
    if (ref.watch(sessionControllerProvider).value == null) {
      return const SizedBox.shrink();
    }
    if (ref.watch(sharedItemOpenableProvider(widget.itemId)).value case final openable?) {
      final l10n = AppLocalizations.of(context);
      return openable
          ? _action(label: l10n.actionOpen, icon: FluentIcons.folder_open_24_regular, onPressed: _open)
          : _action(label: l10n.sharedLinkJoin, icon: FluentIcons.folder_add_24_regular, onPressed: _joining ? null : _join);
    }
    return const SizedBox.shrink();
  }

  Widget _action({required String label, required IconData icon, required VoidCallback? onPressed}) =>
      WindowSizeClass.of(context) == WindowSizeClass.compact
      ? IconButton(tooltip: label, onPressed: onPressed, icon: Icon(icon))
      : TextButton.icon(onPressed: onPressed, icon: Icon(icon), label: Text(label));
}
