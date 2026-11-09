import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/authentication_method.dart';
import '../../app/router/redirect_guard.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../configuration/configuration_controller.dart';
import 'auth_card.dart';
import 'auth_form_controller.dart';
import 'session_controller.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key, this.redirect, this.oidcError});

  final String? redirect;
  final String? oidcError;

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();
  bool _rememberMe = false;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    await ref
        .read(authFormControllerProvider.notifier)
        .login(username: _username.text, password: _password.text, rememberMe: _rememberMe);
  }

  void _loginWithOidc() =>
      ref.read(sessionControllerProvider.notifier).loginWithOidc(returnUrl: safeRedirect(widget.redirect));

  String? _required(String? value) =>
      value == null || value.isEmpty ? AppLocalizations.of(context).loginValueRequired : null;

  AuthFailure? _failure(AsyncValue<void> submission) => switch (submission) {
    AsyncError(error: final AuthFailure failure) => failure,
    AsyncData() when widget.oidcError != null => AuthFailure.ofOidc(widget.oidcError ?? ''),
    _ => null,
  };

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final submission = ref.watch(authFormControllerProvider);
    final configuration = ref.watch(configurationControllerProvider).value;
    final methods = configuration?.authenticationMethods ?? const [AuthenticationMethod.password];
    final passwordLogin = methods.contains(AuthenticationMethod.password);
    final oidcLogin = methods.contains(AuthenticationMethod.oidc);

    return AuthCard(
      formKey: _formKey,
      title: l10n.loginTitle(configuration?.applicationName ?? ''),
      failure: _failure(submission),
      children: [
        if (oidcLogin)
          passwordLogin
              ? OutlinedButton.icon(
                  onPressed: _loginWithOidc,
                  icon: const Icon(FluentIcons.key_24_regular, size: 18),
                  label: Text(l10n.loginWithOidc),
                )
              : FilledButton.icon(
                  onPressed: _loginWithOidc,
                  icon: const Icon(FluentIcons.key_24_regular, size: 18),
                  label: Text(l10n.loginWithOidc),
                ),
        if (oidcLogin && passwordLogin)
          Padding(
            padding: EdgeInsets.symmetric(vertical: spacing),
            child: Row(
              children: [
                const Expanded(child: Divider()),
                Padding(
                  padding: EdgeInsets.symmetric(horizontal: spacing),
                  child: Text(l10n.loginOr),
                ),
                const Expanded(child: Divider()),
              ],
            ),
          ),
        if (passwordLogin) ...[
          TextFormField(
            controller: _username,
            decoration: InputDecoration(labelText: l10n.loginUsername),
            autofillHints: const [AutofillHints.username],
            textInputAction: TextInputAction.next,
            validator: _required,
          ),
          SizedBox(height: spacing),
          TextFormField(
            controller: _password,
            decoration: InputDecoration(labelText: l10n.loginPassword),
            autofillHints: const [AutofillHints.password],
            obscureText: true,
            onFieldSubmitted: (_) => _submit(),
            validator: _required,
          ),
          CheckboxListTile(
            value: _rememberMe,
            onChanged: (value) => setState(() => _rememberMe = value ?? false),
            title: Text(l10n.loginRememberMe),
            controlAffinity: ListTileControlAffinity.leading,
            contentPadding: EdgeInsets.zero,
          ),
          FilledButton(onPressed: submission.isLoading ? null : _submit, child: Text(l10n.loginSubmit)),
        ],
      ],
    );
  }
}
