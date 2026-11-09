import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/user_account.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/layout/window_size_class.dart';
import '../../core/widgets/page_header.dart';
import '../../core/widgets/user_avatar.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/session_controller.dart';
import '../files/folder_states.dart';
import '../files/file_presentation.dart';
import 'user_actions.dart';
import 'users_controller.dart';

class UsersPage extends ConsumerWidget {
  const UsersPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        PageHeader(
          child: Row(
            children: [
              Expanded(child: Text(l10n.userManagement)),
              TextButton.icon(
                onPressed: () => unawaited(UserActions(context).create()),
                icon: const Icon(FluentIcons.person_add_24_regular, size: 18),
                label: Text(l10n.createUser),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        Expanded(
          child: Material(
            type: MaterialType.transparency,
            child: switch (ref.watch(usersControllerProvider)) {
              AsyncValue(:final value?) => compact ? _UserList(users: value) : _UserTable(users: value),
              AsyncValue(error: _?) => FolderMessage(
                icon: FluentIcons.cloud_error_24_regular,
                title: l10n.usersLoadErrorTitle,
                message: l10n.genericError,
                action: FilledButton(
                  onPressed: () => ref.invalidate(usersControllerProvider),
                  child: Text(l10n.errorRetry),
                ),
              ),
              _ => Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
            },
          ),
        ),
      ],
    );
  }
}

class _UserTable extends ConsumerWidget {
  const _UserTable({required this.users});

  static const _maxWidth = 1040.0;
  static const _headerHeight = 52.0;
  static const _rowHeight = 64.0;
  static const _radius = 12.0;

  final List<UserAccount> users;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final colorScheme = Theme.of(context).colorScheme;
    final currentId = ref.watch(sessionControllerProvider).value?.id;
    final border = Border.all(color: colorScheme.outlineVariant);
    final headerStyle = Theme.of(
      context,
    ).textTheme.titleSmall?.copyWith(color: colorScheme.primary, fontWeight: FontWeight.w600);

    return ListView(
      padding: EdgeInsets.all(spacing * 1.5),
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: _maxWidth),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  height: _headerHeight,
                  decoration: BoxDecoration(
                    color: colorScheme.surfaceContainerHigh,
                    border: border,
                    borderRadius: BorderRadius.circular(_radius),
                  ),
                  child: _TableRow(
                    cells: [
                      Text(l10n.loginUsername, style: headerStyle),
                      Text(l10n.registerEmail, style: headerStyle),
                      Text(l10n.userType, style: headerStyle),
                      Text(l10n.userQuotaColumn, style: headerStyle),
                    ],
                  ),
                ),
                SizedBox(height: spacing),
                Container(
                  clipBehavior: Clip.antiAlias,
                  decoration: BoxDecoration(border: border, borderRadius: BorderRadius.circular(_radius)),
                  child: Column(
                    children: [
                      for (final (index, user) in users.indexed)
                        Container(
                          height: _rowHeight,
                          color: index.isEven ? colorScheme.surfaceContainer : colorScheme.surfaceContainerLow,
                          child: _TableRow(
                            cells: [
                              _NameLink(user: user, isCurrent: user.id == currentId),
                              Text(user.email, overflow: TextOverflow.ellipsis),
                              Text(user.isAdmin ? l10n.accountAdministrator : l10n.userTypeUser),
                              _QuotaText(quotaBytes: user.quotaBytes),
                            ],
                            trailing: _UserMenu(user: user, isCurrent: user.id == currentId),
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _TableRow extends StatelessWidget {
  const _TableRow({required this.cells, this.trailing});

  static const _flex = [2, 3, 2, 2];
  static const _trailingWidth = 56.0;

  final List<Widget> cells;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        for (final (index, cell) in cells.indexed)
          Expanded(
            flex: _flex[index],
            child: Center(child: cell),
          ),
        SizedBox(width: _trailingWidth, child: trailing),
      ],
    );
  }
}

class _NameLink extends StatelessWidget {
  const _NameLink({required this.user, required this.isCurrent});

  final UserAccount user;
  final bool isCurrent;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final textTheme = Theme.of(context).textTheme;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Flexible(
          child: InkWell(
            onTap: () => unawaited(UserActions(context).edit(user)),
            child: Text(
              user.username,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(decoration: TextDecoration.underline),
            ),
          ),
        ),
        if (isCurrent) ...[const SizedBox(width: 8), Text(l10n.userCurrent, style: textTheme.labelSmall)],
      ],
    );
  }
}

class _QuotaText extends StatelessWidget {
  const _QuotaText({required this.quotaBytes});

  static const _unlimited = '∞';

  final int? quotaBytes;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return switch (quotaBytes) {
      final int bytes => Text(formatSize(bytes, l10n.localeName)),
      null => Text(_unlimited, semanticsLabel: l10n.userQuotaNoLimit),
    };
  }
}

class _UserList extends ConsumerWidget {
  const _UserList({required this.users});

  final List<UserAccount> users;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final currentId = ref.watch(sessionControllerProvider).value?.id;

    return ListView(
      children: [
        for (final user in users)
          ListTile(
            leading: UserAvatar(userId: user.id, name: user.displayName, hasPicture: user.hasPicture, radius: 20),
            title: Text(user.username, overflow: TextOverflow.ellipsis),
            subtitle: Text(
              [
                user.email,
                if (user.isAdmin) l10n.accountAdministrator,
                if (user.id == currentId) l10n.userCurrent,
              ].join(' · '),
              overflow: TextOverflow.ellipsis,
            ),
            trailing: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                _QuotaText(quotaBytes: user.quotaBytes),
                _UserMenu(user: user, isCurrent: user.id == currentId),
              ],
            ),
            onTap: () => unawaited(UserActions(context).edit(user)),
          ),
      ],
    );
  }
}

enum _UserMenuAction { edit, delete }

class _UserMenu extends StatelessWidget {
  const _UserMenu({required this.user, required this.isCurrent});

  final UserAccount user;
  final bool isCurrent;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return PopupMenuButton<_UserMenuAction>(
      tooltip: l10n.moreActions,
      icon: const Icon(FluentIcons.more_vertical_24_regular),
      onSelected: (action) {
        final actions = UserActions(context);
        unawaited(switch (action) {
          _UserMenuAction.edit => actions.edit(user),
          _UserMenuAction.delete => actions.delete(user),
        });
      },
      itemBuilder: (_) => [
        PopupMenuItem(
          value: _UserMenuAction.edit,
          child: ListTile(leading: const Icon(FluentIcons.edit_24_regular), title: Text(l10n.userEdit)),
        ),
        PopupMenuItem(
          value: _UserMenuAction.delete,
          enabled: !isCurrent,
          child: ListTile(leading: const Icon(FluentIcons.delete_24_regular), title: Text(l10n.userDelete)),
        ),
      ],
    );
  }
}
