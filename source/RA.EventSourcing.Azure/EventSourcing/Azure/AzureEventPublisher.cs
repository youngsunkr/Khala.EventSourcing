﻿namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;
    using static Microsoft.WindowsAzure.Storage.Table.QueryComparisons;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

    public class AzureEventPublisher : IAzureEventPublisher
    {
        private readonly CloudTable _eventTable;
        private readonly JsonMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public AzureEventPublisher(
            CloudTable eventTable,
            JsonMessageSerializer serializer,
            IMessageBus messageBus)
        {
            if (eventTable == null)
            {
                throw new ArgumentNullException(nameof(eventTable));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (messageBus == null)
            {
                throw new ArgumentNullException(nameof(messageBus));
            }

            _eventTable = eventTable;
            _serializer = serializer;
            _messageBus = messageBus;
        }

        public Task PublishPendingEvents<T>(Guid sourceId)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return Publish<T>(sourceId);
        }

        private async Task Publish<T>(Guid sourceId)
            where T : class, IEventSourced
        {
            List<PendingEventTableEntity> pendingEvents =
                await GetPendingEvents<T>(sourceId);

            if (pendingEvents.Any())
            {
                List<IDomainEvent> domainEvents =
                    RestoreDomainEvents(pendingEvents);

                await SendPendingEvents(domainEvents);
                await DeletePendingEvents(pendingEvents);
            }
        }

        private async Task<List<PendingEventTableEntity>> GetPendingEvents<T>(
            Guid sourceId)
            where T : class, IEventSourced
        {
            var query = new TableQuery<PendingEventTableEntity>();

            string filter = GenerateFilterCondition(
                nameof(ITableEntity.PartitionKey),
                Equal,
                PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId));

            return new List<PendingEventTableEntity>(
                await ExecuteQuery(query.Where(filter)));
        }

        private List<IDomainEvent> RestoreDomainEvents(
            List<PendingEventTableEntity> pendingEvents)
        {
            return pendingEvents
                .Select(e => e.PayloadJson)
                .Select(_serializer.Deserialize)
                .Cast<IDomainEvent>()
                .ToList();
        }

        private async Task SendPendingEvents(List<IDomainEvent> domainEvents)
        {
            await _messageBus.SendBatch(domainEvents);
        }

        private async Task DeletePendingEvents(
            List<PendingEventTableEntity> pendingEvents)
        {
            var batch = new TableBatchOperation();
            pendingEvents.ForEach(batch.Delete);
            await _eventTable.ExecuteBatchAsync(batch);
        }

        private async Task<IEnumerable<TEntity>> ExecuteQuery<TEntity>(
            TableQuery<TEntity> query)
            where TEntity : ITableEntity, new()
        {
            var entities = new List<TEntity>();
            TableContinuationToken continuation = null;

            do
            {
                TableQuerySegment<TEntity> segment = await
                    _eventTable.ExecuteQuerySegmentedAsync(query, continuation);
                entities.AddRange(segment);
                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            return entities;
        }
    }
}