#version 450

layout(binding=0)uniform UniformBufferObject{
    vec3 objectColor;
    vec3 lightColor;
    vec3 lightPos;
}ubo;

layout(location=0)in vec3 fNormal;
layout(location=1)in vec3 fPos;

layout(location=0)out vec4 FragColor;

void main(){
    float ambientStrength=.1;
    vec3 ambient=ambientStrength*ubo.lightColor;
    
    vec3 norm=normalize(fNormal);
    vec3 lightDirection=normalize(ubo.lightPos-fPos);
    float diff=max(dot(norm,lightDirection),0.);
    vec3 diffuse=diff*ubo.lightColor;
    
    // The resulting colour should be the amount of ambient colour + the amount of
    // additional colour provided by the diffuse of the lamp
    vec3 result=(ambient+diffuse)*ubo.objectColor;
    
    FragColor=vec4(result,1.);
}
