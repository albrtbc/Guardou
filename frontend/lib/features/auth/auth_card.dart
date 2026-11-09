import 'package:flutter/material.dart';

import '../../app/theme/app_tokens.dart';
import '../../core/layout/window_size_class.dart';
import '../../core/widgets/brand_mark.dart';
import '../../core/widgets/source_code_link.dart';
import '../../l10n/generated/app_localizations.dart';
import 'auth_form_controller.dart';

class AuthCard extends StatelessWidget {
  const AuthCard({
    super.key,
    required this.formKey,
    required this.title,
    this.subtitle,
    required this.failure,
    required this.children,
  });

  static const _maxWidth = 400.0;

  final GlobalKey<FormState> formKey;
  final String title;
  final String? subtitle;
  final AuthFailure? failure;
  final List<Widget> children;

  static String failureMessage(AppLocalizations l10n, AuthFailure failure) => switch (failure) {
    AuthFailure.invalidCredentials => l10n.loginInvalidCredentials,
    AuthFailure.passwordLoginDisabled => l10n.loginPasswordDisabled,
    AuthFailure.rejected => l10n.registerRejected,
    AuthFailure.registrationClosed => l10n.registerClosed,
    AuthFailure.oidcFailed => l10n.loginOidcFailed,
    AuthFailure.oidcNoAccount => l10n.loginOidcNoAccount,
    AuthFailure.oidcRejected => l10n.loginOidcRejected,
    AuthFailure.unknown => l10n.genericError,
  };

  Alignment _alignment(BuildContext context) =>
      WindowSizeClass.of(context) == WindowSizeClass.compact ? Alignment.topCenter : Alignment.center;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final tokens = AppTokens.of(context);
    final theme = Theme.of(context);
    final currentFailure = failure;

    return Scaffold(
      body: Align(
        alignment: _alignment(context),
        child: SingleChildScrollView(
          padding: EdgeInsets.all(tokens.spacing),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: _maxWidth),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Card(
                  child: Padding(
                    padding: EdgeInsets.all(tokens.spacing * 2),
                    child: Form(
                      key: formKey,
                      child: AutofillGroup(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const Center(child: BrandMark(size: 48)),
                            SizedBox(height: tokens.spacing),
                            Text(title, style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
                            if (subtitle case final text?) ...[
                              const SizedBox(height: 8),
                              Text(text, style: theme.textTheme.bodyMedium, textAlign: TextAlign.center),
                            ],
                            SizedBox(height: tokens.spacing * 2),
                            ...children,
                            if (currentFailure != null)
                              Padding(
                                padding: EdgeInsets.only(top: tokens.spacing),
                                child: Text(
                                  failureMessage(l10n, currentFailure),
                                  style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
                const SourceCodeLink(),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
