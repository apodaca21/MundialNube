"""Offline structural checks. These do not claim a live cluster was tested."""
import json
from pathlib import Path
import yaml

root = Path(__file__).resolve().parents[1]
objects=[]
for file in (root/'kubernetes').rglob('*.yaml'):
    for resource in yaml.safe_load_all(file.read_text()):
        assert isinstance(resource,dict), file
        if resource.get('kind') in ('Kustomization','Cluster'):
            continue
        assert 'apiVersion' in resource and 'metadata' in resource, file
        assert resource.get('kind')!='Secret', f'Credentials must not be committed: {file}'
        objects.append(resource)
api=next(x for x in objects if x['kind']=='Deployment' and x['metadata']['name']=='tournaments-api' and 'replicas' in x['spec'])
container=api['spec']['template']['spec']['containers'][0]
assert container['resources']['requests']=={'cpu':'100m','memory':'256Mi'}
assert container['resources']['limits']=={'cpu':'100m','memory':'256Mi'}
assert any(e['name']=='Database__MigrateOnStartup' and e['value']=='false' for e in container['env'])
job=next(x for x in objects if x['kind']=='Job')
assert job['spec']['template']['spec']['containers'][0]['args']==['--migrate']
kustomize=yaml.safe_load((root/'kubernetes/base/kustomization.yaml').read_text())
assert '../migration-job.yaml' not in kustomize['resources'], 'Migration must precede app rollout'
for cm in kustomize['configMapGenerator']:
    for item in cm['files']:
        source=(root/'kubernetes/base'/item.split('=',1)[1]).resolve()
        assert source.is_relative_to(root) and source.is_file(), source
for dashboard in (root/'monitoring/grafana/dashboards').glob('*.json'):
    data=json.loads(dashboard.read_text()); assert data['uid'] and data['panels']
compose=yaml.safe_load((root/'compose.v3.yaml').read_text())
assert compose['services']['api']['depends_on']['migrate']['condition']=='service_completed_successfully'
assert compose['services']['consumer']['command']==['--consume']
for target in [root/'.github/workflows/v3-ci.yml',root/'monitoring/v3/tempo.yaml',root/'monitoring/v3/prometheus-kubernetes.yaml']:
    yaml.safe_load(target.read_text())
print(f'Validated {len(objects)} Kubernetes resources, Compose ordering, dashboard JSON and configuration files.')
