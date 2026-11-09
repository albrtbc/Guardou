import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/task_queue_summary.dart';
import '../../api/generated/models/task_state.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/layout/window_size_class.dart';
import '../../core/widgets/page_header.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/folder_states.dart';
import 'task_actions.dart';
import 'task_list_dialog.dart';
import 'task_presentation.dart';
import 'tasks_controller.dart';

class TasksPage extends ConsumerStatefulWidget {
  const TasksPage({super.key});

  static const refreshInterval = Duration(seconds: 5);

  @override
  ConsumerState<TasksPage> createState() => _TasksPageState();
}

class _TasksPageState extends ConsumerState<TasksPage> {
  late final Timer _refresh;

  @override
  void initState() {
    super.initState();
    _refresh = Timer.periodic(TasksPage.refreshInterval, (_) => ref.invalidate(taskQueuesProvider));
  }

  @override
  void dispose() {
    _refresh.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        PageHeader(child: Text(l10n.taskQueue)),
        const Divider(height: 1),
        Expanded(
          child: Material(
            type: MaterialType.transparency,
            child: switch (ref.watch(taskQueuesProvider)) {
              AsyncValue(:final value?) => _QueueList(queues: value),
              AsyncValue(error: _?) => FolderMessage(
                icon: FluentIcons.cloud_error_24_regular,
                title: l10n.tasksLoadErrorTitle,
                message: l10n.genericError,
                action: FilledButton(onPressed: () => ref.invalidate(taskQueuesProvider), child: Text(l10n.errorRetry)),
              ),
              _ => Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
            },
          ),
        ),
      ],
    );
  }
}

class _QueueList extends StatelessWidget {
  const _QueueList({required this.queues});

  static const _maxWidth = 800.0;

  final List<TaskQueueSummary> queues;

  @override
  Widget build(BuildContext context) {
    final spacing = AppTokens.of(context).spacing;
    return ListView.separated(
      padding: EdgeInsets.all(spacing * 1.5),
      itemCount: queues.length,
      separatorBuilder: (_, _) => SizedBox(height: spacing * 1.5),
      itemBuilder: (context, index) => Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: _maxWidth),
          child: QueueCard(queue: queues[index]),
        ),
      ),
    );
  }
}

class QueueCard extends StatelessWidget {
  const QueueCard({super.key, required this.queue});

  static const _actionsWidth = 112.0;

  final TaskQueueSummary queue;

  @override
  Widget build(BuildContext context) {
    final tokens = AppTokens.of(context);
    final colorScheme = Theme.of(context).colorScheme;
    final compact = WindowSizeClass.of(context) == WindowSizeClass.compact;
    final content = Padding(
      padding: EdgeInsets.all(tokens.spacing * 1.5),
      child: _QueueContent(queue: queue),
    );
    final actions = _QueueActions(queue: queue, vertical: !compact);

    return Material(
      color: colorScheme.surfaceContainer,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(tokens.panelRadius)),
      child: compact
          ? Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [content, actions])
          : IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(child: content),
                  SizedBox(width: _actionsWidth, child: actions),
                ],
              ),
            ),
    );
  }
}

class _QueueContent extends StatelessWidget {
  const _QueueContent({required this.queue});

  final TaskQueueSummary queue;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final spacing = AppTokens.of(context).spacing;
    final titleStyle = theme.textTheme.titleLarge?.copyWith(
      color: theme.colorScheme.primary,
      fontWeight: FontWeight.w600,
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            Icon(queueIcon(queue.kind), color: theme.colorScheme.primary),
            SizedBox(width: spacing / 2),
            Flexible(child: Text(queueTitle(queue.kind, l10n), style: titleStyle)),
          ],
        ),
        SizedBox(height: spacing / 2),
        Text(queueDescription(queue.kind, l10n), style: theme.textTheme.bodyMedium),
        SizedBox(height: spacing),
        _QueueCounters(queue: queue),
      ],
    );
  }
}

class _QueueCounters extends StatelessWidget {
  const _QueueCounters({required this.queue});

  static const _height = 56.0;
  static const _radius = 12.0;

  final TaskQueueSummary queue;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colorScheme = Theme.of(context).colorScheme;
    final counters = [
      (TaskState.running, queue.running, colorScheme.primaryContainer, colorScheme.onPrimaryContainer),
      (TaskState.pending, queue.pending, colorScheme.secondaryContainer, colorScheme.onSecondaryContainer),
      (TaskState.failed, queue.failed, colorScheme.errorContainer, colorScheme.onErrorContainer),
    ];

    return ClipRRect(
      borderRadius: BorderRadius.circular(_radius),
      child: SizedBox(
        height: _height,
        child: Row(
          children: [
            for (final (state, count, background, foreground) in counters)
              Expanded(
                child: _Counter(
                  label: taskStateLabel(state, l10n),
                  count: count,
                  background: background,
                  foreground: foreground,
                  onTap: () => unawaited(showTaskListDialog(context, kind: queue.kind, state: state)),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _Counter extends StatelessWidget {
  const _Counter({
    required this.label,
    required this.count,
    required this.background,
    required this.foreground,
    required this.onTap,
  });

  final String label;
  final int count;
  final Color background;
  final Color foreground;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final spacing = AppTokens.of(context).spacing;
    return Material(
      color: background,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: spacing),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: textTheme.bodyLarge?.copyWith(color: foreground),
                ),
              ),
              Text('$count', style: textTheme.headlineSmall?.copyWith(color: foreground)),
            ],
          ),
        ),
      ),
    );
  }
}

class _QueueActions extends StatelessWidget {
  const _QueueActions({required this.queue, required this.vertical});

  final TaskQueueSummary queue;
  final bool vertical;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final buttons = [
      _ActionTile(
        icon: FluentIcons.arrow_repeat_all_24_regular,
        label: l10n.queueAll,
        onTap: () => unawaited(TaskActions(context).queueAll(queue.kind)),
      ),
    ];

    return ColoredBox(
      color: Theme.of(context).colorScheme.surfaceContainerHighest,
      child: vertical
          ? Column(children: [for (final button in buttons) Expanded(child: button)])
          : Row(children: [for (final button in buttons) Expanded(child: button)]),
    );
  }
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.icon, required this.label, required this.onTap});

  static const _minHeight = 64.0;

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: ConstrainedBox(
        constraints: const BoxConstraints(minHeight: _minHeight),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, color: Theme.of(context).colorScheme.onSurface),
            const SizedBox(height: 4),
            Text(label, style: Theme.of(context).textTheme.labelMedium),
          ],
        ),
      ),
    );
  }
}
