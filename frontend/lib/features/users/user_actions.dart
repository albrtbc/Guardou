import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../api/generated/models/user_account.dart';
import '../../app/router/routes.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/session_controller.dart';
import '../files/dialogs/confirm_dialog.dart';
import 'user_editor_dialog.dart';
import 'user_failure.dart';
import 'users_controller.dart';
import 'users_service.dart';

class UserActions {
  UserActions(this._context)
    : _container = ProviderScope.containerOf(_context, listen: false),
      _messenger = ScaffoldMessenger.of(_context),
      _router = GoRouter.of(_context),
      _l10n = AppLocalizations.of(_context);

  final BuildContext _context;
  final ProviderContainer _container;
  final ScaffoldMessengerState _messenger;
  final GoRouter _router;
  final AppLocalizations _l10n;

  Future<void> create() async {
    if (await showUserEditorDialog(_context) != null) {
      _show(_l10n.userCreated);
    }
  }

  Future<void> edit(UserAccount account) async {
    final saved = await showUserEditorDialog(_context, account: account);
    if (saved == null) {
      return;
    }
    _show(_l10n.userSaved);
    if (saved.id == _container.read(sessionControllerProvider).value?.id) {
      if (!saved.isAdmin) {
        _router.go(Routes.root);
      }
      await _container.read(sessionControllerProvider.notifier).refresh();
    }
  }

  Future<void> delete(UserAccount account) async {
    final confirmed = await showConfirmDialog(
      _context,
      title: _l10n.userDeleteTitle(account.username),
      message: _l10n.userDeleteMessage,
      confirmLabel: _l10n.userDelete,
    );
    if (!confirmed) {
      return;
    }
    try {
      await _container.read(usersServiceProvider).delete(account.id);
      _show(_l10n.userDeleted);
    } on DioException catch (error) {
      _show(UserFailure.of(error).message(_l10n));
    } finally {
      _container.invalidate(usersControllerProvider);
    }
  }

  void _show(String message) => _messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}
