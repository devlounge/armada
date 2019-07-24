package util

import (
	"github.com/G-Research/k8s-batch/internal/common"
	"github.com/stretchr/testify/assert"
	v1 "k8s.io/api/core/v1"
	"k8s.io/apimachinery/pkg/api/resource"
	"testing"
)

func TestCalculateTotalResource(t *testing.T) {
	resources := makeDefaultResource()
	node1 := makeNodeWithResource(resources)
	node2 := makeNodeWithResource(resources)

	//Expected is resources * 2 nodes
	expectedResult := common.FromResourceList(resources)
	expectedResult.Add(expectedResult)

	result := CalculateTotalResource([]*v1.Node{&node1, &node2})
	assert.Equal(t, result, expectedResult)
}

func TestCalculateTotalResourceLimit_ShouldSumAllContainers(t *testing.T) {
	resources := makeDefaultResource()
	pod := makePodWthResource([]*v1.ResourceList{&resources, &resources})

	//Expected is resources * 2 containers
	expectedResult := common.FromResourceList(resources)
	expectedResult.Add(expectedResult)

	result := CalculateTotalResourceLimit([]*v1.Pod{&pod})
	assert.Equal(t, result, expectedResult)
}

func TestCalculateTotalResourceLimit_ShouldSumAllPods(t *testing.T) {
	resources := makeDefaultResource()
	pod1 := makePodWthResource([]*v1.ResourceList{&resources})
	pod2 := makePodWthResource([]*v1.ResourceList{&resources})

	//Expected is resources * 2 pods
	expectedResult := common.FromResourceList(resources)
	expectedResult.Add(expectedResult)

	result := CalculateTotalResourceLimit([]*v1.Pod{&pod1, &pod2})
	assert.Equal(t, result, expectedResult)
}

func makeDefaultResource() v1.ResourceList {
	cpuResource := resource.NewMilliQuantity(100, resource.DecimalSI)
	memoryResource := resource.NewMilliQuantity(50*1024*1024*1024, resource.DecimalSI)
	storageResource := resource.NewMilliQuantity(500*1024*1024*1024, resource.DecimalSI)
	ephemeralStorageResource := resource.NewMilliQuantity(20*1024*1024*1024, resource.DecimalSI)
	resourceMap := map[v1.ResourceName]resource.Quantity{
		v1.ResourceCPU:              *cpuResource,
		v1.ResourceMemory:           *memoryResource,
		v1.ResourceStorage:          *storageResource,
		v1.ResourceEphemeralStorage: *ephemeralStorageResource,
	}
	return resourceMap
}

func makePodWthResource(resources []*v1.ResourceList) v1.Pod {
	containers := make([]v1.Container, len(resources))
	for i, res := range resources {
		containers[i] = v1.Container{
			Resources: v1.ResourceRequirements{
				Limits: *res,
			},
		}
	}
	pod := v1.Pod{
		Spec: v1.PodSpec{
			Containers: containers,
		},
	}

	return pod
}

func makeNodeWithResource(resources map[v1.ResourceName]resource.Quantity) v1.Node {
	node := v1.Node{
		Status: v1.NodeStatus{
			Allocatable: resources,
		},
	}
	return node
}
