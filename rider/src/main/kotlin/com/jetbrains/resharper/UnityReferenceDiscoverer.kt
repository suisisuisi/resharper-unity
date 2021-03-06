package com.jetbrains.resharper

import com.intellij.openapi.project.Project
import com.intellij.util.EventDispatcher
import com.jetbrains.resharper.projectView.solution
import com.jetbrains.resharper.util.idea.ILifetimedComponent
import com.jetbrains.resharper.util.idea.LifetimedComponent
import com.jetbrains.resharper.util.idea.application
import com.jetbrains.rider.model.RdAssemblyReferenceDescriptor
import com.jetbrains.rider.model.RdProjectModelItemDescriptor

class UnityReferenceDiscoverer(project: Project) : ILifetimedComponent by LifetimedComponent(project) {
    private val myProjectModelView = project.solution.projectModelView
    private val myEventDispatcher = EventDispatcher.create(UnityReferenceListener::class.java)

    init {
        application.invokeLater {
            myProjectModelView.items.advise(componentLifetime) { item ->
                val itemData = item.newValueOpt
                if (itemData == null) {
                    // Item removed. Don't care about this. It's a weird scenario if someone
                    // removes a Unity project
                }
                else {
                    // Item added or updated
                    itemAddedOrUpdated(itemData.descriptor)
                }
            }
        }
    }

    private fun itemAddedOrUpdated(descriptor: RdProjectModelItemDescriptor) {
        if (descriptor is RdAssemblyReferenceDescriptor && descriptor.name == "UnityEngine") {
            myEventDispatcher.multicaster.HasUnityReference()
        }
    }

    fun addUnityReferenceListener(listener: UnityReferenceListener) {
        myEventDispatcher.addListener(listener)
    }
}
