// Archived n7, S L45239-45261. Do not saturate the source negative/over-one
// brightness: fixed-function blending clamps blend factors at the attachment.
float HillsReflectorBrightness(float3 world,float3 normal,float3 lightPosition,float3 lightDirection)
{
    float3 car=lightPosition-world;float d=length(car);float b=0;
    if(d<150){float distanceFactor=1-(d-75)/75;float angle=max(0,dot(normal,lightDirection));float cone=(dot(lightDirection,normalize(car))-.90)/.1;b=distanceFactor*angle*cone;}
    return b;
}
