﻿using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Music.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NzbDrone.Core.Music
{
    public interface ITrackService
    {
        Track GetTrack(int id);
        List<Track> GetTracks(IEnumerable<int> ids);
        Track FindTrack(string artistId, string albumId, int trackNumber);
        Track FindTrackByTitle(string artistId, string albumId, string releaseTitle);
        List<Track> GetTracksByArtist(string artistId);
        //List<Track> GetTracksByAlbum(string artistId, string albumId);
        //List<Track> GetTracksByAlbumTitle(string artistId, string albumTitle);
        List<Track> TracksWithFiles(string artistId);
        //PagingSpec<Track> TracksWithoutFiles(PagingSpec<Track> pagingSpec);
        List<Track> GetTracksByFileId(int trackFileId);
        void UpdateTrack(Track track);
        void SetTrackMonitored(int trackId, bool monitored);
        void UpdateTracks(List<Track> tracks);
        void InsertMany(List<Track> tracks);
        void UpdateMany(List<Track> tracks);
        void DeleteMany(List<Track> tracks);
        void SetTrackMonitoredByAlbum(string artistId, string albumId, bool monitored);
    }

    public class TrackService : ITrackService
    {
        private readonly ITrackRepository _trackRepository;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public TrackService(ITrackRepository trackRepository, IConfigService configService, Logger logger)
        {
            _trackRepository = trackRepository;
            _configService = configService;
            _logger = logger;
        }

        public Track GetTrack(int id)
        {
            return _trackRepository.Get(id);
        }

        public List<Track> GetTracks(IEnumerable<int> ids)
        {
            return _trackRepository.Get(ids).ToList();
        }

        public Track FindTrack(string artistId, string albumId, int episodeNumber)
        {
            return _trackRepository.Find(artistId, albumId, episodeNumber);
        }

        public List<Track> GetTracksByArtist(string artistId)
        {
            return _trackRepository.GetTracks(artistId).ToList();
        }

        public List<Track> GetTracksByAlbum(string artistId, string albumId)
        {
            return _trackRepository.GetTracks(artistId, albumId);
        }

        public Track FindTrackByTitle(string artistId, string albumId, string releaseTitle)
        {
            // TODO: can replace this search mechanism with something smarter/faster/better
            var normalizedReleaseTitle = Parser.Parser.NormalizeEpisodeTitle(releaseTitle).Replace(".", " ");
            var tracks = _trackRepository.GetTracks(artistId, albumId);

            var matches = tracks.Select(
                track => new
                {
                    Position = normalizedReleaseTitle.IndexOf(Parser.Parser.NormalizeEpisodeTitle(track.Title), StringComparison.CurrentCultureIgnoreCase),
                    Length = Parser.Parser.NormalizeEpisodeTitle(track.Title).Length,
                    Track = track
                })
                                .Where(e => e.Track.Title.Length > 0 && e.Position >= 0)
                                .OrderBy(e => e.Position)
                                .ThenByDescending(e => e.Length)
                                .ToList();

            if (matches.Any())
            {
                return matches.First().Track;
            }

            return null;
        }

        public List<Track> TracksWithFiles(string artistId)
        {
            return _trackRepository.TracksWithFiles(artistId);
        }


        public PagingSpec<Track> TracksWithoutFiles(PagingSpec<Track> pagingSpec)
        {
            var episodeResult = _trackRepository.TracksWithoutFiles(pagingSpec);

            return episodeResult;
        }

        public List<Track> GetTracksByFileId(int trackFileId)
        {
            return _trackRepository.GetTracksByFileId(trackFileId);
        }

        public void UpdateTrack(Track track)
        {
            _trackRepository.Update(track);
        }

        public void SetTrackMonitored(int trackId, bool monitored)
        {
            var track = _trackRepository.Get(trackId);
            _trackRepository.SetMonitoredFlat(track, monitored);

            _logger.Debug("Monitored flag for Track:{0} was set to {1}", trackId, monitored);
        }

        public void SetTrackMonitoredByAlbum(string artistId, string albumId, bool monitored)
        {
            _trackRepository.SetMonitoredByAlbum(artistId, albumId, monitored);
        }

        public void UpdateEpisodes(List<Track> tracks)
        {
            _trackRepository.UpdateMany(tracks);
        }

        public void InsertMany(List<Track> tracks)
        {
            _trackRepository.InsertMany(tracks);
        }

        public void UpdateMany(List<Track> tracks)
        {
            _trackRepository.UpdateMany(tracks);
        }

        public void DeleteMany(List<Track> tracks)
        {
            _trackRepository.DeleteMany(tracks);
        }

        public void HandleAsync(ArtistDeletedEvent message)
        {
            var tracks = GetTracksByArtist(message.Artist.SpotifyId);
            _trackRepository.DeleteMany(tracks);
        }

        public void Handle(TrackFileDeletedEvent message)
        {
            foreach (var track in GetTracksByFileId(message.TrackFile.Id))
            {
                _logger.Debug("Detaching track {0} from file.", track.Id);
                track.TrackFileId = 0;

                if (message.Reason != DeleteMediaFileReason.Upgrade && _configService.AutoUnmonitorPreviouslyDownloadedEpisodes)
                {
                    track.Monitored = false;
                }

                UpdateTrack(track);
            }
        }

        public void Handle(TrackFileAddedEvent message)
        {
            foreach (var track in message.TrackFile.Tracks.Value)
            {
                _trackRepository.SetFileId(track.Id, message.TrackFile.Id);
                _logger.Debug("Linking [{0}] > [{1}]", message.TrackFile.RelativePath, track);
            }
        }

        public void UpdateTracks(List<Track> tracks)
        {
            _trackRepository.UpdateMany(tracks);
        }
    }
}
