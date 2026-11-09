import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/task_entry.dart';
import '../../api/generated/models/task_kind.dart';
import '../../api/generated/models/task_state.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/layout/window_size_class.dart';
import '../../l10n/generated/app_localizations.dart';
import 'task_actions.dart';
import 'task_presentation.dart';
import 'tasks_controller.dart';

Future<void> showTaskListDialog(BuildContext context, {required TaskKind kind, required TaskState state}) {
  final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;
  final list = _TaskListView(kind: kind, state: state);
  return compact
      ? Navigator.of(context).push(
          MaterialPageRoute<void>(
            fullscreenDialog: true,
            builder: (context) => ColoredBox(color: Theme.of(context).colorScheme.surface, child: list),
          ),
        )
      : showDialog<void>(
          context: context,
          builder: (_) => Dialog(child: list),
        );
}

class _TaskListView extends ConsumerWidget {
  const _TaskListView({required this.kind, required this.state});

  static const _maxWidth = 640.0;
  static const _maxHeight = 560.0;

  final TaskKind kind;
  final TaskState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final tasks = ref.watch(taskListProvider((kind: kind, state: state)));
    final title = l10n.taskListTitle(queueTitle(kind, l10n), taskStateLabel(state, l10n));
    final hasFailed = state == TaskState.failed && (tasks.value?.isNotEmpty ?? false);

    final spacing = AppTokens.of(context).spacing;

    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: _maxWidth, maxHeight: _maxHeight),
      child: Material(
        type: MaterialType.transparency,
        child: SafeArea(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: EdgeInsetsDirectional.fromSTEB(spacing * 1.5, spacing / 2, spacing / 2, spacing / 2),
                child: Row(
                  children: [
                    Expanded(child: Text(title, style: Theme.of(context).textTheme.titleLarge)),
                    if (hasFailed)
                      TextButton.icon(
                        onPressed: () => unawaited(TaskActions(context).resetFailed(kind)),
                        icon: const Icon(FluentIcons.arrow_counterclockwise_24_regular, size: 18),
                        label: Text(l10n.tasksResetFailed),
                      ),
                    IconButton(
                      tooltip: l10n.close,
                      onPressed: () => Navigator.of(context).pop(),
                      icon: const Icon(FluentIcons.dismiss_24_regular),
                    ),
                  ],
                ),
              ),
              const Divider(height: 1),
              Flexible(
                child: switch (tasks) {
                  AsyncValue(value: final entries?) when entries.isEmpty => Padding(
                    padding: EdgeInsets.all(spacing * 2),
                    child: Center(child: Text(l10n.taskListEmpty)),
                  ),
                  AsyncValue(:final value?) => ListView(
                    shrinkWrap: true,
                    children: [for (final task in value) _TaskTile(task: task)],
                  ),
                  AsyncValue(error: _?) => Center(child: Text(l10n.tasksLoadErrorTitle)),
                  _ => Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TaskTile extends StatelessWidget {
  const _TaskTile({required this.task});

  final TaskEntry task;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return ListTile(
      title: Text(task.itemName.isEmpty ? l10n.myFiles : task.itemName, overflow: TextOverflow.ellipsis),
      subtitle: Text(
        [task.owner, taskPriorityLabel(task.priority, l10n), taskUpdatedLabel(task, l10n), ?task.error].join(' · '),
        maxLines: 3,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: _TaskMenu(task: task),
    );
  }
}

enum _TaskMenuAction { run, reset }

class _TaskMenu extends StatelessWidget {
  const _TaskMenu({required this.task});

  final TaskEntry task;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return PopupMenuButton<_TaskMenuAction>(
      tooltip: l10n.moreActions,
      icon: const Icon(FluentIcons.more_vertical_24_regular),
      onSelected: (action) {
        final actions = TaskActions(context);
        unawaited(switch (action) {
          _TaskMenuAction.run => actions.run(task),
          _TaskMenuAction.reset => actions.reset(task),
        });
      },
      itemBuilder: (_) => [
        PopupMenuItem(
          value: _TaskMenuAction.run,
          enabled: canRun(task),
          child: ListTile(leading: const Icon(FluentIcons.play_24_regular), title: Text(l10n.taskRunNow)),
        ),
        PopupMenuItem(
          value: _TaskMenuAction.reset,
          enabled: canReset(task),
          child: ListTile(
            leading: const Icon(FluentIcons.arrow_counterclockwise_24_regular),
            title: Text(l10n.taskReset),
          ),
        ),
      ],
    );
  }
}
