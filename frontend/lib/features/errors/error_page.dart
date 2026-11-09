import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/routes.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/session_controller.dart';
import '../configuration/configuration_controller.dart';

enum ErrorKind {
  forbidden(403),
  notFound(404),
  serverError(500);

  const ErrorKind(this.statusCode);

  final int statusCode;

  String title(AppLocalizations l10n) => switch (this) {
    forbidden => l10n.errorForbiddenTitle,
    notFound => l10n.errorNotFoundTitle,
    serverError => l10n.errorServerTitle,
  };

  String message(AppLocalizations l10n) => switch (this) {
    forbidden => l10n.errorForbiddenMessage,
    notFound => l10n.errorNotFoundMessage,
    serverError => l10n.errorServerMessage,
  };
}

class ErrorPage extends ConsumerWidget {
  const ErrorPage({super.key, required this.kind});

  final ErrorKind kind;

  void _retry(WidgetRef ref) {
    ref.invalidate(configurationControllerProvider);
    ref.invalidate(sessionControllerProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final tokens = AppTokens.of(context);
    final textTheme = Theme.of(context).textTheme;

    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: EdgeInsets.all(tokens.spacing),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('${kind.statusCode}', style: textTheme.displayLarge),
              SizedBox(height: tokens.spacing),
              Text(kind.title(l10n), style: textTheme.headlineSmall, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              Text(kind.message(l10n), textAlign: TextAlign.center),
              SizedBox(height: tokens.spacing * 2),
              Wrap(
                spacing: tokens.spacing,
                runSpacing: tokens.spacing,
                alignment: WrapAlignment.center,
                children: [
                  if (kind == ErrorKind.serverError)
                    FilledButton(onPressed: () => _retry(ref), child: Text(l10n.errorRetry)),
                  OutlinedButton(onPressed: () => context.go(Routes.root), child: Text(l10n.errorGoToStartPage)),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
