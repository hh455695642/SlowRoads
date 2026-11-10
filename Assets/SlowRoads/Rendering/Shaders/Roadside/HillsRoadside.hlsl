// Hills material update: bt.emissiveIntensity=Math.min(sun.intensity/20,.5).
float HillsSignEmission(float sourceSunIntensity){return min(sourceSunIntensity/20,.5);}
