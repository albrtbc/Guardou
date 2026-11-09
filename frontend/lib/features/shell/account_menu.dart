import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/widgets/user_avatar.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/session_controller.dart';
import '../profile/profile_picture_editor.dart';

class AccountMenu extends ConsumerWidget {
  const AccountMenu({super.key});

  static const _maxWidth = 360.0;
  static const _margin = 8.0;

  void _open(BuildContext context) {
    unawaited(
      showGeneralDialog<void>(
        context: context,
        barrierDismissible: true,
        barrierLabel: AppLocalizations.of(context).accountMenu,
        barrierColor: Colors.transparent,
        pageBuilder: (context, _, _) => SafeArea(
          child: Align(
            alignment: AlignmentDirectional.topEnd,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(_margin, kToolbarHeight, _margin, _margin),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: _maxWidth),
                child: const AccountPanel(),
              ),
            ),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(sessionControllerProvider).value;
    if (user == null) {
      return const SizedBox.shrink();
    }

    return IconButton(
      tooltip: AppLocalizations.of(context).accountMenu,
      onPressed: () => _open(context),
      icon: UserAvatar(userId: user.id, name: user.displayName, hasPicture: user.hasPicture, radius: 16),
    );
  }
}

class AccountPanel extends ConsumerWidget {
  const AccountPanel({super.key});

  static const _outerRadius = 28.0;
  static const _innerRadius = 24.0;
  static const _avatarRadius = 40.0;
  static const _signOutHeight = 56.0;

  void _signOut(BuildContext context, WidgetRef ref) {
    Navigator.of(context).pop();
    unawaited(ref.read(sessionControllerProvider.notifier).logout());
  }

  void _openAdministration(BuildContext context) => _go(context, Routes.adminUsers);

  void _openSettings(BuildContext context) => _go(context, Routes.accountSettings);

  void _go(BuildContext context, String location) {
    final router = GoRouter.of(context);
    Navigator.of(context).pop();
    router.go(location);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(sessionControllerProvider).value;
    if (user == null) {
      return const SizedBox.shrink();
    }
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final textTheme = Theme.of(context).textTheme;
    final colorScheme = Theme.of(context).colorScheme;

    return Material(
      color: colorScheme.surfaceContainer,
      elevation: 3,
      borderRadius: BorderRadius.circular(_outerRadius),
      child: Padding(
        padding: EdgeInsets.all(spacing),
        child: Material(
          color: colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(_innerRadius),
          clipBehavior: Clip.antiAlias,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: EdgeInsets.all(spacing * 1.5),
                child: Column(
                  children: [
                    ProfilePictureEditor(user: user, radius: _avatarRadius),
                    SizedBox(height: spacing),
                    Text(
                      user.displayName,
                      style: textTheme.titleLarge?.copyWith(color: colorScheme.primary, fontWeight: FontWeight.w600),
                      textAlign: TextAlign.center,
                    ),
                    Text(user.email, style: textTheme.bodyMedium, textAlign: TextAlign.center),
                    if (user.isAdmin)
                      Text(
                        l10n.accountAdministrator,
                        style: textTheme.labelSmall?.copyWith(color: colorScheme.onSurfaceVariant),
                      ),
                    SizedBox(height: spacing),
                    FilledButton.tonalIcon(
                      onPressed: () => _openSettings(context),
                      icon: const Icon(FluentIcons.settings_24_regular, size: 18),
                      label: Text(l10n.accountSettings),
                    ),
                    if (user.isAdmin) ...[
                      SizedBox(height: spacing / 2),
                      FilledButton.tonalIcon(
                        onPressed: () => _openAdministration(context),
                        icon: const Icon(FluentIcons.shield_checkmark_24_regular, size: 18),
                        label: Text(l10n.administration),
                      ),
                    ],
                  ],
                ),
              ),
              Divider(height: 2, thickness: 2, color: colorScheme.surfaceContainer),
              TextButton.icon(
                onPressed: () => _signOut(context, ref),
                style: TextButton.styleFrom(
                  foregroundColor: colorScheme.onSurface,
                  minimumSize: const Size.fromHeight(_signOutHeight),
                  shape: const RoundedRectangleBorder(),
                ),
                icon: const Icon(FluentIcons.sign_out_24_regular, size: 18),
                label: Text(l10n.signOut),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
