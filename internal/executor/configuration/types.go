package configuration

import (
	"time"

	"github.com/G-Research/armada/internal/common/client"
)

type ApplicationConfiguration struct {
	ClusterId string
}

type KubernetesConfiguration struct {
	ImpersonateUsers  bool
	TrackedNodeLabels []string
}

type TaskConfiguration struct {
	UtilisationReportingInterval          time.Duration
	MissingJobEventReconciliationInterval time.Duration
	JobLeaseRenewalInterval               time.Duration
	AllocateSpareClusterCapacityInterval  time.Duration
	StuckPodScanInterval                  time.Duration
	PodDeletionInterval                   time.Duration
	PodMetricsInterval                    time.Duration
}

type ExecutorConfiguration struct {
	MetricsPort   uint16
	Application   ApplicationConfiguration
	ApiConnection client.ApiConnectionDetails

	Kubernetes KubernetesConfiguration
	Task       TaskConfiguration
}
