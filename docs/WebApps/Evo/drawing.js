

class EvoDrawing
{
    constructor(canvas, imgNames, resultsName, networkoutputName, offscreenCanvasName){
        this.canvas = canvas;
        this.context = canvas.getContext("2d");

        this.networkoutputName = networkoutputName;
        this.paused=false;
        this.resultsLabelName = resultsName;
        this.trackImageNames = imgNames;
        this.offscreenCanvasName = offscreenCanvasName;

        this._SwitchToImage(0);

        this.switchToImgAtNextGen = -1;

        this.entities = [];

        this.entityCountTarget = 60;
        this.entityTimeoutS = 500;
        this.frameTimeS = 0.05; 
        this.generationSurvivorPercentage = 0.2;
        this.learningRate = 0.01;
        this.outlierChance = 0.02;
        this.outlierDelta = 20;

        this.drawFpsTarget = 60;
        this.drawFrameTimeTargetMs  = 1000 / this.drawFpsTarget;
        this.lastFrameTime = Date.now();
        this.simsPerTick = 1;

        this.bestLapS = this.entityTimeoutS;
        this.currentSessionTimer = 0;
        this.currentGeneration = 1;
        this.simulationSpeed = 1;
        this.timerFnc = undefined;
        this.loadNetworkInNextFrame = null;
        
        while( this.entities.length < this.entityCountTarget ){
            this.entities.push(new Entity(this.GetPixelAtPoint.bind(this)));
        }
        this.prevTopPerformer = this.entities[0].brain.GetNetworkAsJSON();
        
        this._MutateEntities();

        this.SetSimulationSpeed(this.simulationSpeed);
    }

    SwitchImageAtNextGeneration(idx)
    {
        this.switchToImgAtNextGen = idx;
    }

    _SwitchToImage(idx)
    {
        this.bestLapS = this.entityTimeoutS;

        this.currentImageIndex = idx % this.trackImageNames.length;
        let img = document.getElementById(this.trackImageNames[idx].imageName);
        EntityCheckpoints = this.trackImageNames[idx].checkpoints;
        EntityDefaultState = this.trackImageNames[idx].defaultState;
        this.offscreenCanvas = document.getElementById(this.offscreenCanvasName);
        this.imgWidth = img.width;
        this.offscreenCanvas.width = img.width;
        this.offscreenCanvas.height = img.height;
        this.offscreenCanvas.getContext('2d').drawImage(img, 0, 0, img.width, img.height);
        this.offscreenCanvasContext = this.offscreenCanvas.getContext('2d');
        let offscreenCanvasImageRGBA = this.offscreenCanvasContext.getImageData(0, 0, img.width, img.height);
        this.offscreenCanvasImage = [];
        for(let iy = 0; iy < img.height; iy++){
            for(let ix = 0; ix < img.width; ix++){
                this.offscreenCanvasImage[iy * img.width + ix] = ( offscreenCanvasImageRGBA.data[(iy * img.width + ix)*4] ) > 0.2 ? true : false;
            }   
        }
    }

    _MutateEntities(skipFirst = false) {
        let elementCount = -1;
        for(let entity of this.entities){
            elementCount++;
            if (elementCount == 0 && skipFirst) 
                continue;
            entity.Mutate(this.learningRate);
        }
    }

    GetLastTopPerformerAsJSON(){
        return this.prevTopPerformer;
    }

    _Timeout() {
        for(let entity of this.entities){
            entity.GenerationEnd();
        }

        this.entities.sort( function(a,b){ return b.reward-a.reward; } ); //Sort by descending order
        
        this.prevTopPerformer = this.entities[0].brain.GetNetworkAsJSON();
        if ( this.networkoutputName  != undefined ){
            let networkAsString = this.prevTopPerformer;
            document.getElementById(this.networkoutputName).innerHTML = "//Generation " + this.currentGeneration + ", Reward: " + (this.entities[0].reward|0)
             +"\n\n" +  networkAsString;
        }

        this.entities = this.entities.slice(0, Math.floor(this.entities.length * this.generationSurvivorPercentage) );
        
        let survivorCount = this.entities.length;
        let currentEntityIdx = 0;
        while(this.entities.length < this.entityCountTarget){
            this.entities.push(this.entities[currentEntityIdx].DeepCopy());
            currentEntityIdx = (currentEntityIdx+1)%survivorCount;
        }

        this._MutateEntities(true);

        if (this.switchToImgAtNextGen >= 0) {
            this._SwitchToImage(this.switchToImgAtNextGen);
            for(let entity of this.entities){
                entity.ResetBestLap();
            }
            this.switchToImgAtNextGen = -1;
        }

        for(let entity of this.entities){
            entity.Reset();
        }
        
        this.currentSessionTimer = 0;
        this.currentGeneration++;
    }

    LoadNetworkFromJSON(jsonStr){
        let nnObj = JSON.parse(jsonStr);
        this.loadNetworkInNextFrame = new NeuralNetwork(nnObj);
    }

    SetSurvivalRate(rate){ this.generationSurvivorPercentage = rate; }

    SetMutationRate(rate){ this.learningRate = rate; }

    SetSimulationSpeed(speed){
        this.simulationSpeed = speed;
        if (this.timerFnc)
            clearInterval(this.timerFnc);
        
        let setIntervalMsLimit = 10;
        this.paused = this.simulationSpeed == 0;
        
        if ( this.simulationSpeed > 0 ){
            let tickTimeMs = (this.frameTimeS * 1000) / this.simulationSpeed;
            if ( tickTimeMs < setIntervalMsLimit )
            {
                this.simsPerTick = (setIntervalMsLimit / tickTimeMs)|0;
                tickTimeMs = setIntervalMsLimit;
            } else{
                this.simsPerTick = 1;
            }

            this.timerFnc = setInterval(()=>{this.Tick()}, tickTimeMs);
        } else {
            this.timerFnc = setInterval(()=>{this.Tick()}, 250);
		}
    }

    Simulate(dt) {
        if ( this.paused )
            return;

        let allEntitiesDisqualified = this.entities.every( (entity)=>{return entity.IsDisqualified()} );

        this.currentSessionTimer += dt;
        if ( this.currentSessionTimer >= this.entityTimeoutS || allEntitiesDisqualified){
            this._Timeout();
        }

        for(let entity of this.entities){
            entity.Process(dt);
        }

    }

    Draw() {
        var img = document.getElementById(this.trackImageNames[this.currentImageIndex].imageName);
        this.context.drawImage(img, 0, 0);
        
        for(let entity of this.entities){
            entity.Draw(this.context);
        }
        
        var resultsPanel = document.getElementById(this.resultsLabelName);

        let bestLapSEntities = Math.min.apply( Math, this.entities.map( function(o){ return o.bestLapS; } ) );
        this.bestLapS = Math.min(this.bestLapS, bestLapSEntities);
        let bestLapString = this.bestLapS >= this.entityTimeoutS ? "---" : (this.bestLapS.toFixed(2) + "s");

        resultsPanel.innerText = "Generation: " + this.currentGeneration + 
         "\nCars in race: " + this.entities.filter(x => !x.IsDisqualified()).length +
         "\nTimer: " + (this.currentSessionTimer|0) + "s" +
         "\nBest lap: " + bestLapString;
    }

    Tick(){
        if (this.loadNetworkInNextFrame){
            this._LoadPendingNetwork();
        }

        for( let i = 0; i < this.simsPerTick; ++i){
            this.Simulate(this.frameTimeS);

            let currentTime = Date.now();
            if (currentTime - this.lastFrameTime >= this.drawFrameTimeTargetMs){
                this.Draw();
                this.lastFrameTime = currentTime;
            }
        }
    }

    GetPixelAtPoint(x,y)
    {
        return this.offscreenCanvasImage[(y|0)*this.imgWidth+(x|0)];
    }

    _LoadPendingNetwork(){
        try {
            let newEntities = [ new Entity( this.GetPixelAtPoint.bind(this), this.loadNetworkInNextFrame) ];
            
            while(newEntities.length < this.entityCountTarget){
                newEntities.push(newEntities[0].DeepCopy());
            }

            this.entities = newEntities;
            this._MutateEntities(true);
            document.getElementById("lblClipboardResult").innerHTML = "Loaded network from clipboard!";
            this.loadNetworkInNextFrame = null;
        } catch (error) {
            this.loadNetworkInNextFrame = null;
        }
    }
}

