import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/api/api_problem.dart';
import '../../l10n/generated/app_localizations.dart';

class FolderMessage extends StatelessWidget {
  const FolderMessage({super.key, required this.icon, required this.title, required this.message, this.action});

  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final tokens = AppTokens.of(context);
    final theme = Theme.of(context);

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(tokens.spacing),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 64, color: theme.colorScheme.outline),
            SizedBox(height: tokens.spacing),
            Text(title, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
            const SizedBox(height: 4),
            Text(message, textAlign: TextAlign.center),
            if (action case final button?) ...[SizedBox(height: tokens.spacing), button],
          ],
        ),
      ),
    );
  }
}

class EmptyFolderView extends StatelessWidget {
  const EmptyFolderView({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return FolderMessage(
      icon: FluentIcons.folder_open_24_regular,
      title: l10n.emptyFolderTitle,
      message: l10n.emptyFolderMessage,
    );
  }
}

class FolderErrorView extends StatelessWidget {
  const FolderErrorView({super.key, required this.isRoot, required this.error, required this.onRetry});

  final bool isRoot;
  final Object error;
  final VoidCallback onRetry;

  bool get _isNotFound => switch (error) {
    DioException(error: ApiProblem(status: 404)) => true,
    _ => false,
  };

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    if (_isNotFound) {
      return FolderMessage(
        icon: FluentIcons.folder_prohibited_24_regular,
        title: l10n.folderNotFoundTitle,
        message: l10n.folderNotFoundMessage,
        action: isRoot ? null : OutlinedButton(onPressed: () => context.go(Routes.files), child: Text(l10n.openRoot)),
      );
    }
    return FolderMessage(
      icon: FluentIcons.cloud_error_24_regular,
      title: l10n.folderLoadErrorTitle,
      message: l10n.genericError,
      action: FilledButton(onPressed: onRetry, child: Text(l10n.errorRetry)),
    );
  }
}
