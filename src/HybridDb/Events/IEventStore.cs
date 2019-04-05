using System;
using System.Collections.Generic;

namespace HybridDb.Events
{
    public interface IEventStore
    {
        IObservable<Commit<byte[]>> Commits { get; }

        Commit<byte[]> Save(PreparedCommit prepare);

        Position GetPositionOf(Guid commitId);
        IEnumerable<Commit<byte[]>> LoadCommits(params Guid[] ids);

        /// <summary>
        /// Returns parent commit if commitId is found.
        /// If commitId is not found (or is null), head of the
        /// commits will be returned.
        /// The empty commit does not have a parent, and returns null
        /// </summary>
        /// <param name="commitId"></param>
        /// <returns></returns>
        Commit<byte[]> LoadParentCommit(Guid? commitId);
        
        IEnumerable<EventData<byte[]>> Load(string id, long fromStreamSeq, long toPosition = long.MaxValue, Direction direction = Direction.Forward);
        IEnumerable<Commit<byte[]>> Stream(long fromPositionIncluding);

        void Import(IEnumerable<PreparedCommit> prepares);
    }
}