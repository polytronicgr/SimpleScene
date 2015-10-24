﻿using System;
using System.Collections.Generic;
using System.Drawing; // for RectangleF
using OpenTK;

namespace SimpleScene.Demos
{
    public class SSpaceMissilesSimulation
    {
        public static Random rand = new Random();

        public float timeScale = 1f;
        //public float timeScale = 0.3f;

        protected readonly List<SSpaceMissileClusterData> _clusters
            = new List<SSpaceMissileClusterData>();

        #region targets
        protected readonly HashSet<ISSpaceMissileTarget> _targets = new HashSet<ISSpaceMissileTarget>();
        protected float _timeDeltaAccumulator = 0f;
        public float targetUpdateInterval = 0.2f;
        #endregion

        public int numClusters { get { return _clusters.Count; } }

        public SSpaceMissileClusterData launchCluster(
            Vector3 launchPos, Vector3 launchVel, int numMissiles,
            ISSpaceMissileTarget target, float timeToHit,
            SSpaceMissileParameters clusterParams)
        {
            var cluster = new SSpaceMissileClusterData (
              launchPos, launchVel, numMissiles, target, timeToHit, clusterParams);
            _clusters.Add(cluster);
            _targets.Add(target);
            return cluster;
        }

        public void removeMissile(SSpaceMissileData missile)
        {
            missile.terminate();
        }

        public void removeCluster(SSpaceMissileClusterData cluster)
        {
            cluster.terminateAll();
        }

        public void removeAll()
        {
            foreach (var cluster in _clusters) {
                cluster.terminateAll();
            }
            _clusters.Clear();
        }

        public void updateSimulation(float timeElapsed)
        {
            timeElapsed *= timeScale;

            // update targets
            float accTime = timeElapsed + _timeDeltaAccumulator;
            while (accTime >= targetUpdateInterval) {
                foreach (var target in _targets) {
                    target.update(targetUpdateInterval);
                }
                accTime -= targetUpdateInterval;
            }
            _timeDeltaAccumulator = accTime;

            // update clusters/missiles
            foreach (var cluster in _clusters) {
                cluster.updateSimulation(timeElapsed);
            }
            _clusters.RemoveAll( (cluster) => cluster.isTerminated );
        }
    }

    public class SSpaceMissileClusterData
    {
        public SSpaceMissileData[] missiles { get { return _missiles; } }
        public SSpaceMissileParameters parameters { get { return _parameters; } }
        public ISSpaceMissileTarget target { get { return _target; } }
        public float timeToHit { get { return _timeToHit; } }
        public float timeSinceLaunch { get { return _timeSinceLaunch; } }
        public bool isTerminated { get { return _isTerminated; } }

        protected readonly SSpaceMissileData[] _missiles;
        protected readonly ISSpaceMissileTarget _target;
        protected readonly SSpaceMissileParameters _parameters;

        protected float _timeDeltaAccumulator = 0f;
        protected float _timeSinceLaunch = 0f;
        protected float _timeToHit = 0f;
        protected bool _isTerminated = false;

        public SSpaceMissileClusterData(
            Vector3 launcherPos, Vector3 launcherVel, int numMissiles,
            ISSpaceMissileTarget target, float timeToHit,
            SSpaceMissileParameters mParams)
        {
            _target = target;
            _timeToHit = timeToHit;
            _parameters = mParams;
            _missiles = new SSpaceMissileData[numMissiles];

            Matrix4 spawnTxfm = _parameters.spawnTxfm(_target, launcherPos, launcherVel);
            if (_parameters.spawnGenerator != null) {
                _parameters.spawnGenerator.Generate(numMissiles,
                    (i, scale, pos, orient) => {
                        Vector3 missilePos = pos * _parameters.spawnDistanceScale;
                        missilePos = Vector3.Transform(missilePos, spawnTxfm);
                        _missiles [i] = new SSpaceMissileData (
                            this, i, launcherPos, launcherVel, missilePos, timeToHit);
                        return true; // accept new missile from the generator
                    }
                );
            } else {
                for (int i = 0; i < numMissiles; ++i) {
                    var missilePos = Vector3.Transform(Vector3.Zero, spawnTxfm);
                    _missiles [i] = new SSpaceMissileData (
                        this, i, launcherPos, launcherVel, missilePos, timeToHit);
                }
            }
        }

        public void updateTimeToHit(float timeToHit)
        {
            _timeToHit = timeToHit;
        }

        public void terminateAll()
        {
            foreach (var missile in _missiles) {
                missile.terminate();
            }
            _isTerminated = true;
        }

        public void updateSimulation(float timeElapsed)
        {
            float accTime = timeElapsed + _timeDeltaAccumulator;
            float step = parameters.simulationStep;
            while (accTime >= step) {
                _simulateStep(step);
                accTime -= step;
            }
            _timeDeltaAccumulator = accTime;
        }

        protected void _simulateStep(float timeElapsed)
        {
            bool isTerminated = true;
            foreach (var missile in _missiles) {
                if (missile.state != SSpaceMissileData.State.Terminated) {
                    isTerminated = false;
                    missile.updateExecution(timeElapsed);
                }
            }
            _isTerminated = isTerminated;
            _timeToHit -= timeElapsed;
            _timeSinceLaunch += timeElapsed;
        }
    }
}

